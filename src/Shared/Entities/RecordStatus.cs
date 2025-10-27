namespace SecureHumanLoopCaptcha.Shared.Entities;

public enum RecordStatus
{
    Queued = 0,
    InProgress = 1,
    AwaitingHuman = 2,
    HumanClaimed = 3,
    Resumed = 4,
    Completed = 5,
    Failed = 6
}
