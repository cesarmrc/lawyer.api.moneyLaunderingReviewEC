using System.Text.Json;

namespace SecureHumanLoopCaptcha.Shared.Messaging;

public record HumanActionMessage(Guid RecordId, JsonElement Inputs, string? Notes, string OperatorId);
