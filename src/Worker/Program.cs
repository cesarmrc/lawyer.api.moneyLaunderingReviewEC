using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SecureHumanLoopCaptcha.Shared.Data;
using SecureHumanLoopCaptcha.Shared.Security;
using SecureHumanLoopCaptcha.Worker.Services;
using Serilog;
using StackExchange.Redis;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddLogging(logging => logging.AddSerilog());

var encryptionKey = builder.Configuration["Encryption:Key"]
    ?? throw new InvalidOperationException("Encryption key configuration is required.");
var encryptionIv = builder.Configuration["Encryption:IV"]
    ?? throw new InvalidOperationException("Encryption IV configuration is required.");

builder.Services.AddSingleton<IEncryptionService>(_ => new AesEncryptionService(encryptionKey, encryptionIv));

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? builder.Configuration["Database:ConnectionString"]
    ?? "Host=postgres;Port=5432;Database=automation;Username=automation;Password=automation";

builder.Services.AddDbContext<AutomationDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")
        ?? builder.Configuration["Redis:ConnectionString"]
        ?? "redis:6379"));

builder.Services.AddSingleton<SnapshotStorage>();
builder.Services.AddSingleton<HumanActionChannel>();
builder.Services.AddHostedService<RedisHumanActionListener>();
builder.Services.AddHostedService<AutomationWorker>();

var host = builder.Build();

await host.RunAsync();
