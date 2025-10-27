using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace SecureHumanLoopCaptcha.Shared.Dto;

public class IntakeRequest
{
    [Required]
    public JsonElement Payload { get; set; }

    public string? Source { get; set; }
}
