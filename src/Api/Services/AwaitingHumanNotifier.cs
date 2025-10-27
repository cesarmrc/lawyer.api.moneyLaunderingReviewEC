using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using SecureHumanLoopCaptcha.Api.Hubs;
using SecureHumanLoopCaptcha.Shared.Dto;
using SecureHumanLoopCaptcha.Shared.Entities;
using SecureHumanLoopCaptcha.Shared.Extensions;
using SecureHumanLoopCaptcha.Shared.Messaging;
using SecureHumanLoopCaptcha.Shared.Security;
using StackExchange.Redis;

namespace SecureHumanLoopCaptcha.Api.Services;

public class AwaitingHumanNotifier
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly IHubContext<JobsHub, IJobsClient> _hubContext;
    private readonly IEncryptionService _encryptionService;

    public AwaitingHumanNotifier(
        IConnectionMultiplexer connectionMultiplexer,
        IHubContext<JobsHub, IJobsClient> hubContext,
        IEncryptionService encryptionService)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _hubContext = hubContext;
        _encryptionService = encryptionService;
    }

    public async Task NotifyAwaitingAsync(AutomationRecord record)
    {
        var response = record.ToResponse(_encryptionService);
        var payload = JsonSerializer.Serialize(response, SerializerOptions);
        var subscriber = _connectionMultiplexer.GetSubscriber();
        await subscriber.PublishAsync(MessagingChannels.AwaitingHuman, payload);
        await subscriber.PublishAsync(MessagingChannels.StatusUpdates, payload);
        await _hubContext.Clients.All.JobAwaiting(response);
    }

    public async Task NotifyUpdateAsync(AutomationRecord record)
    {
        var response = record.ToResponse(_encryptionService);
        var payload = JsonSerializer.Serialize(response, SerializerOptions);
        var subscriber = _connectionMultiplexer.GetSubscriber();
        await subscriber.PublishAsync(MessagingChannels.StatusUpdates, payload);
        await _hubContext.Clients.All.JobUpdated(response);
    }
}
