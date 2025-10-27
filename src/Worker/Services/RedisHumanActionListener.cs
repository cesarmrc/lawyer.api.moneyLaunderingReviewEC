using StackExchange.Redis;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SecureHumanLoopCaptcha.Shared.Messaging;

namespace SecureHumanLoopCaptcha.Worker.Services;

public class RedisHumanActionListener : BackgroundService
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly HumanActionChannel _channel;
    private readonly ILogger<RedisHumanActionListener> _logger;

    public RedisHumanActionListener(
        IConnectionMultiplexer connectionMultiplexer,
        HumanActionChannel channel,
        ILogger<RedisHumanActionListener> logger)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _channel = channel;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscriber = _connectionMultiplexer.GetSubscriber();
        return subscriber.SubscribeAsync(MessagingChannels.HumanActions, (channel, value) =>
        {
            try
            {
                var message = JsonSerializer.Deserialize<HumanActionMessage>(value!, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (message is not null)
                {
                    _channel.Publish(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize human action message");
            }
        });
    }
}
