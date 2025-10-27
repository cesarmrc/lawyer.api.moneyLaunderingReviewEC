using StackExchange.Redis;
using SecureHumanLoopCaptcha.Shared.Dto;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using SecureHumanLoopCaptcha.Shared.Data;
using SecureHumanLoopCaptcha.Shared.Entities;
using SecureHumanLoopCaptcha.Shared.Extensions;
using SecureHumanLoopCaptcha.Shared.Messaging;
using SecureHumanLoopCaptcha.Shared.Security;
using SecureHumanLoopCaptcha.Worker.Automation;

namespace SecureHumanLoopCaptcha.Worker.Services;

public class AutomationWorker : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceProvider _serviceProvider;
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly SnapshotStorage _snapshotStorage;
    private readonly HumanActionChannel _humanActionChannel;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<AutomationWorker> _logger;

    public AutomationWorker(
        IServiceProvider serviceProvider,
        IConnectionMultiplexer connectionMultiplexer,
        SnapshotStorage snapshotStorage,
        HumanActionChannel humanActionChannel,
        IEncryptionService encryptionService,
        ILogger<AutomationWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _connectionMultiplexer = connectionMultiplexer;
        _snapshotStorage = snapshotStorage;
        _humanActionChannel = humanActionChannel;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscriber = _connectionMultiplexer.GetSubscriber();
        return subscriber.SubscribeAsync(MessagingChannels.JobQueue, (channel, value) =>
        {
            var message = JsonSerializer.Deserialize<JobQueueMessage>(value!, SerializerOptions);
            if (message is null)
            {
                return;
            }

            _ = Task.Run(() => ProcessJobAsync(message.RecordId, stoppingToken), stoppingToken);
        });
    }

    private async Task ProcessJobAsync(Guid recordId, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AutomationDbContext>();

            var record = await dbContext.Records.Include(r => r.Actions).FirstOrDefaultAsync(r => r.Id == recordId, stoppingToken);
            if (record is null)
            {
                _logger.LogWarning("Record {RecordId} not found", recordId);
                return;
            }

            record.Status = RecordStatus.InProgress;
            record.Actions.Add(new RecordAction
            {
                Actor = "worker",
                ActionType = "started"
            });
            await dbContext.SaveChangesAsync(stoppingToken);

            await using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var page = await browser.NewPageAsync();

            var targetUrl = ExtractTargetUrl(record);
            if (string.IsNullOrWhiteSpace(targetUrl))
            {
                await MarkFailedAsync(dbContext, record, "Missing targetUrl in payload", stoppingToken);
                return;
            }

            await page.GotoAsync(targetUrl);

            if (await CaptchaDetector.DetectAsync(page))
            {
                await HandleCaptchaAsync(dbContext, record, page, stoppingToken);
            }
            else
            {
                await CompleteRecordAsync(dbContext, record, page, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing record {RecordId}", recordId);
            await using var scope = _serviceProvider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AutomationDbContext>();
            var record = await dbContext.Records
                .Include(r => r.Actions)
                .FirstOrDefaultAsync(r => r.Id == recordId, stoppingToken);
            if (record is not null)
            {
                record.Status = RecordStatus.Failed;
                record.Actions.Add(new RecordAction
                {
                    Actor = "worker",
                    ActionType = "failed",
                    Notes = ex.Message
                });
                await dbContext.SaveChangesAsync(stoppingToken);

                var subscriber = _connectionMultiplexer.GetSubscriber();
                var response = record.ToResponse(_encryptionService);
                await subscriber.PublishAsync(MessagingChannels.StatusUpdates, JsonSerializer.Serialize(response, SerializerOptions));
            }
        }
    }

    private string? ExtractTargetUrl(AutomationRecord record)
    {
        using var payload = record.GetPayload(_encryptionService);
        if (payload is null)
        {
            return null;
        }

        if (payload.RootElement.TryGetProperty("targetUrl", out var urlElement) && urlElement.ValueKind == JsonValueKind.String)
        {
            return urlElement.GetString();
        }

        return null;
    }

    private async Task HandleCaptchaAsync(AutomationDbContext dbContext, AutomationRecord record, IPage page, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Captcha detected for record {RecordId}", record.Id);

        var screenshot = await page.ScreenshotAsync(new PageScreenshotOptions { FullPage = true });
        var screenshotPath = await _snapshotStorage.SaveScreenshotAsync(screenshot, record.Id.ToString(), cancellationToken);
        var html = await page.ContentAsync();
        var htmlPath = await _snapshotStorage.SaveHtmlAsync(html, record.Id.ToString(), cancellationToken);

        record.Status = RecordStatus.AwaitingHuman;
        record.ScreenshotPath = screenshotPath;
        record.HtmlSnapshotPath = htmlPath;
        record.Actions.Add(new RecordAction
        {
            Actor = "worker",
            ActionType = "captcha_detected"
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        var subscriber = _connectionMultiplexer.GetSubscriber();
        var response = record.ToResponse(_encryptionService);
        var serialized = JsonSerializer.Serialize(response, SerializerOptions);
        await subscriber.PublishAsync(MessagingChannels.AwaitingHuman, serialized);
        await subscriber.PublishAsync(MessagingChannels.StatusUpdates, serialized);

        var message = await _humanActionChannel.WaitForActionAsync(record.Id, cancellationToken);
        await ApplyHumanInputAsync(page, message.Inputs);

        record.Status = RecordStatus.Resumed;
        record.Actions.Add(new RecordAction
        {
            Actor = message.OperatorId,
            ActionType = "human_input",
            Notes = message.Notes
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        await CompleteRecordAsync(dbContext, record, page, cancellationToken);
    }

    private static async Task ApplyHumanInputAsync(IPage page, JsonElement inputs)
    {
        if (inputs.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (inputs.TryGetProperty("fields", out var fields) && fields.ValueKind == JsonValueKind.Array)
        {
            foreach (var field in fields.EnumerateArray())
            {
                if (field.TryGetProperty("selector", out var selectorElement) && selectorElement.ValueKind == JsonValueKind.String)
                {
                    var selector = selectorElement.GetString();
                    if (string.IsNullOrWhiteSpace(selector))
                    {
                        continue;
                    }

                    var value = field.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.String
                        ? valueElement.GetString()
                        : null;

                    if (value is not null)
                    {
                        await page.FillAsync(selector!, value);
                    }
                }
            }
        }

        if (inputs.TryGetProperty("clickSelector", out var clickElement) && clickElement.ValueKind == JsonValueKind.String)
        {
            var selector = clickElement.GetString();
            if (!string.IsNullOrWhiteSpace(selector))
            {
                await page.ClickAsync(selector!);
            }
        }
    }

    private async Task CompleteRecordAsync(AutomationDbContext dbContext, AutomationRecord record, IPage page, CancellationToken cancellationToken)
    {
        var screenshot = await page.ScreenshotAsync(new PageScreenshotOptions { FullPage = true });
        record.ScreenshotPath = await _snapshotStorage.SaveScreenshotAsync(screenshot, record.Id.ToString() + "_final", cancellationToken);
        record.Status = RecordStatus.Completed;
        record.Actions.Add(new RecordAction
        {
            Actor = "worker",
            ActionType = "completed"
        });

        record.ResultUrl = page.Url;
        await dbContext.SaveChangesAsync(cancellationToken);

        var subscriber = _connectionMultiplexer.GetSubscriber();
        var response = record.ToResponse(_encryptionService);
        await subscriber.PublishAsync(MessagingChannels.StatusUpdates, JsonSerializer.Serialize(response, SerializerOptions));
    }

    private async Task MarkFailedAsync(AutomationDbContext dbContext, AutomationRecord record, string reason, CancellationToken cancellationToken)
    {
        record.Status = RecordStatus.Failed;
        record.Actions.Add(new RecordAction
        {
            Actor = "worker",
            ActionType = "failed",
            Notes = reason
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        var subscriber = _connectionMultiplexer.GetSubscriber();
        var response = record.ToResponse(_encryptionService);
        await subscriber.PublishAsync(MessagingChannels.StatusUpdates, JsonSerializer.Serialize(response, SerializerOptions));
    }
}
