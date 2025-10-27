using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SecureHumanLoopCaptcha.Shared.Dto;

namespace SecureHumanLoopCaptcha.Api.Hubs;

[Authorize]
public class JobsHub : Hub<IJobsClient>
{
}

public interface IJobsClient
{
    Task JobAwaiting(RecordResponse record);

    Task JobUpdated(RecordResponse record);
}
