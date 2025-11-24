using System.Text.Json;
using SecureHumanLoopCaptcha.Shared.Dto;
using SecureHumanLoopCaptcha.Shared.Entities;
using SecureHumanLoopCaptcha.Shared.Security;

namespace SecureHumanLoopCaptcha.Shared.Extensions;

public static class RecordMappingExtensions
{
    public static RecordResponse ToResponse(this AutomationRecord record, IEncryptionService encryptionService)
    {
        object? payload = null;

        using var payloadDoc = record.GetPayload(encryptionService);
        if (payloadDoc != null)
        {
            payload = JsonSerializer.Deserialize<object>(payloadDoc.RootElement.GetRawText());
        }

        var actions = record.Actions
            .OrderBy(a => a.CreatedUtc)
            .Select(a => new RecordActionResponse(a.Actor, a.ActionType, a.Notes, a.CreatedUtc))
            .ToList();

        return new RecordResponse(
            record.Id,
            record.Status,
            record.CreatedUtc,
            record.UpdatedUtc,
            record.Source,
            record.ResultUrl,
            record.ScreenshotPath,
            record.HtmlSnapshotPath,
            payload,
            actions);
    }
}
