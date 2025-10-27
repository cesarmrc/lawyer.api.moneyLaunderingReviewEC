namespace SecureHumanLoopCaptcha.Shared.Entities;

public class RecordAction
{
    public int Id { get; set; }

    public Guid RecordId { get; set; }

    public AutomationRecord? Record { get; set; }

    public string Actor { get; set; } = string.Empty;

    public string ActionType { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
