using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureHumanLoopCaptcha.Api.Hubs;
using SecureHumanLoopCaptcha.Api.Services;
using SecureHumanLoopCaptcha.Shared.Data;
using SecureHumanLoopCaptcha.Shared.Dto;
using SecureHumanLoopCaptcha.Shared.Entities;
using SecureHumanLoopCaptcha.Shared.Extensions;
using SecureHumanLoopCaptcha.Shared.Messaging;
using SecureHumanLoopCaptcha.Shared.Security;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});

var encryptionKey = builder.Configuration["Encryption:Key"]
    ?? throw new InvalidOperationException("Encryption key configuration is required.");
var encryptionIv = builder.Configuration["Encryption:IV"]
    ?? throw new InvalidOperationException("Encryption IV configuration is required.");

builder.Services.AddSingleton<IEncryptionService>(_ => new AesEncryptionService(encryptionKey, encryptionIv));

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? builder.Configuration["Database:ConnectionString"]
    ?? "Host=postgres;Port=5432;Database=automation;Username=automation;Password=automation";

builder.Services.AddDbContext<AutomationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")
        ?? builder.Configuration["Redis:ConnectionString"]
        ?? "redis:6379"));

builder.Services.AddScoped<JobQueuePublisher>();
builder.Services.AddScoped<AwaitingHumanNotifier>();

builder.Services.AddSignalR();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Oidc:Authority"] ?? "https://example.com";
        options.Audience = builder.Configuration["Oidc:Audience"] ?? "secure-human-loop";
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidateIssuer = true;
    });

builder.Services.AddAuthorization();

builder.Services.Configure<JsonOptions>(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPost("/intake", async (
    [FromBody] IntakeRequest request,
    AutomationDbContext dbContext,
    IEncryptionService encryptionService,
    JobQueuePublisher publisher,
    CancellationToken cancellationToken) =>
{
    if (request.Payload.ValueKind == JsonValueKind.Undefined || request.Payload.ValueKind == JsonValueKind.Null)
    {
        return Results.BadRequest(new { message = "Payload is required." });
    }

    var record = new AutomationRecord
    {
        Source = request.Source,
        Status = RecordStatus.Queued
    };

    record.ApplyPayload(request.Payload, encryptionService);
    record.Actions.Add(new RecordAction
    {
        Actor = "system",
        ActionType = "intake",
        Notes = "Payload accepted"
    });

    dbContext.Records.Add(record);
    await dbContext.SaveChangesAsync(cancellationToken);

    await publisher.PublishJobAsync(new JobQueueMessage(record.Id));

    return Results.Created($"/records/{record.Id}", new { record.Id });
})
.RequireAuthorization();

app.MapGet("/records/{id:guid}", async (
    Guid id,
    AutomationDbContext dbContext,
    IEncryptionService encryptionService,
    CancellationToken cancellationToken) =>
{
    var record = await dbContext.Records
        .Include(r => r.Actions)
        .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    return record is null
        ? Results.NotFound()
        : Results.Ok(record.ToResponse(encryptionService));
})
.RequireAuthorization();

app.MapGet("/jobs/awaiting", async (
    AutomationDbContext dbContext,
    IEncryptionService encryptionService,
    CancellationToken cancellationToken) =>
{
    var records = await dbContext.Records
        .Include(r => r.Actions)
        .Where(r => r.Status == RecordStatus.AwaitingHuman || r.Status == RecordStatus.HumanClaimed)
        .OrderBy(r => r.CreatedUtc)
        .ToListAsync(cancellationToken);

    var response = records.Select(r => r.ToResponse(encryptionService));
    return Results.Ok(response);
})
.RequireAuthorization();

app.MapPost("/jobs/{id:guid}/claim", async (
    Guid id,
    [FromBody] ClaimRequest request,
    AutomationDbContext dbContext,
    AwaitingHumanNotifier notifier,
    CancellationToken cancellationToken) =>
{
    var record = await dbContext.Records
        .Include(r => r.Actions)
        .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    if (record is null)
    {
        return Results.NotFound();
    }

    if (record.Status != RecordStatus.AwaitingHuman)
    {
        return Results.BadRequest(new { message = "Record is not awaiting a human operator." });
    }

    record.Status = RecordStatus.HumanClaimed;
    record.Actions.Add(new RecordAction
    {
        Actor = request.OperatorId,
        ActionType = "claim"
    });

    await dbContext.SaveChangesAsync(cancellationToken);
    await notifier.NotifyUpdateAsync(record);

    return Results.Ok(record.Id);
})
.RequireAuthorization();

app.MapPost("/jobs/{id:guid}/human-action", async (
    Guid id,
    [FromBody] HumanActionRequest request,
    AutomationDbContext dbContext,
    AwaitingHumanNotifier notifier,
    IConnectionMultiplexer connectionMultiplexer,
    CancellationToken cancellationToken) =>
{
    var record = await dbContext.Records
        .Include(r => r.Actions)
        .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    if (record is null)
    {
        return Results.NotFound();
    }

    if (record.Status != RecordStatus.HumanClaimed)
    {
        return Results.BadRequest(new { message = "Record must be claimed before submitting human input." });
    }

    record.Status = RecordStatus.Resumed;
    record.Actions.Add(new RecordAction
    {
        Actor = request.OperatorId,
        ActionType = "human_input",
        Notes = request.Notes
    });

    await dbContext.SaveChangesAsync(cancellationToken);

    var message = new HumanActionMessage(id, request.Inputs, request.Notes, request.OperatorId);
    var payload = JsonSerializer.Serialize(message, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    await connectionMultiplexer.GetSubscriber().PublishAsync(MessagingChannels.HumanActions, payload);

    await notifier.NotifyUpdateAsync(record);

    return Results.Accepted($"/records/{id}");
})
.RequireAuthorization();

app.MapHub<JobsHub>("/hubs/jobs");

app.Run();
