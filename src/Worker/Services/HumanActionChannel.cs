using System.Collections.Concurrent;
using SecureHumanLoopCaptcha.Shared.Messaging;

namespace SecureHumanLoopCaptcha.Worker.Services;

public class HumanActionChannel
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<HumanActionMessage>> _pending = new();

    public Task<HumanActionMessage> WaitForActionAsync(Guid recordId, CancellationToken cancellationToken)
    {
        var tcs = _pending.GetOrAdd(recordId, _ => new TaskCompletionSource<HumanActionMessage>(TaskCreationOptions.RunContinuationsAsynchronously));

        cancellationToken.Register(() =>
        {
            if (tcs.TrySetCanceled(cancellationToken))
            {
                _pending.TryRemove(recordId, out _);
            }
        });

        return tcs.Task;
    }

    public void Publish(HumanActionMessage message)
    {
        if (_pending.TryRemove(message.RecordId, out var tcs))
        {
            tcs.TrySetResult(message);
        }
    }
}
