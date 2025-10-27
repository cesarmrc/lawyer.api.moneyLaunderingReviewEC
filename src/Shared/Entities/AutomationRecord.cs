using System.Text.Json;
using SecureHumanLoopCaptcha.Shared.Security;

namespace SecureHumanLoopCaptcha.Shared.Entities;

public class AutomationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public RecordStatus Status { get; set; } = RecordStatus.Queued;

    public string EncryptedPayload { get; set; } = string.Empty;

    public string? Source { get; set; }

    public string? ScreenshotPath { get; set; }

    public string? HtmlSnapshotPath { get; set; }

    public string? ResultUrl { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public List<RecordAction> Actions { get; set; } = new();

    public void ApplyPayload(JsonElement payload, IEncryptionService encryptionService)
    {
        EncryptedPayload = encryptionService.Encrypt(payload.GetRawText());
        UpdatedUtc = DateTime.UtcNow;
    }

    public JsonDocument? GetPayload(IEncryptionService encryptionService)
    {
        if (string.IsNullOrWhiteSpace(EncryptedPayload))
        {
            return null;
        }

        var json = encryptionService.Decrypt(EncryptedPayload);
        return JsonDocument.Parse(json);
    }
}
