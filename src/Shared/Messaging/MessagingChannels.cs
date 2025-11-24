namespace SecureHumanLoopCaptcha.Shared.Messaging;

public static class MessagingChannels
{
    public const string JobQueue = "automation-jobs";
    public const string AwaitingHuman = "automation-awaiting-human";
    public const string HumanActions = "automation-human-actions";
    public const string StatusUpdates = "automation-status-updates";
}
