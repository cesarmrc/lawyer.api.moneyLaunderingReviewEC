using SecureHumanLoopCaptcha.Shared.Entities;

namespace SecureHumanLoopCaptcha.Shared.Dto;

public record RecordResponse(
    Guid Id,
    RecordStatus Status,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    string? Source,
    string? ResultUrl,
    string? ScreenshotPath,
    string? HtmlSnapshotPath,
    object? Payload,
    IReadOnlyCollection<RecordActionResponse> Actions);

public record RecordActionResponse(
    string Actor,
    string ActionType,
    string? Notes,
    DateTime CreatedUtc);
