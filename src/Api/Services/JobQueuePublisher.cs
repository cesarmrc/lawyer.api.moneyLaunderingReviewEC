using System.Text.Json;
using SecureHumanLoopCaptcha.Shared.Messaging;
using StackExchange.Redis;

namespace SecureHumanLoopCaptcha.Api.Services;

public class JobQueuePublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public JobQueuePublisher(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    public async Task PublishJobAsync(JobQueueMessage message)
    {
        var payload = JsonSerializer.Serialize(message, SerializerOptions);
        await _connectionMultiplexer.GetSubscriber().PublishAsync(MessagingChannels.JobQueue, payload);
    }
}
