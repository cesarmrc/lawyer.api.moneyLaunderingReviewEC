using System.ComponentModel.DataAnnotations;

namespace SecureHumanLoopCaptcha.Shared.Dto;

public class ClaimRequest
{
    [Required]
    [MaxLength(128)]
    public string OperatorId { get; set; } = string.Empty;
}
