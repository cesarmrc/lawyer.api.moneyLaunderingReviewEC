using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace SecureHumanLoopCaptcha.Shared.Dto;

public class HumanActionRequest
{
    [Required]
    [MaxLength(128)]
    public string OperatorId { get; set; } = string.Empty;

    [Required]
    public JsonElement Inputs { get; set; }

    public string? Notes { get; set; }
}
