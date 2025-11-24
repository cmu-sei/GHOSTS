using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ghosts.Pandora.Infrastructure.Models;

[Table("law_enforcement_requests")]
public class LawEnforcementRequest
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string RequestingAgency { get; set; }

    [Required]
    [MaxLength(100)]
    public string CaseNumber { get; set; }

    [Required]
    [MaxLength(50)]
    public string RequestType { get; set; }

    [Required]
    [MaxLength(500)]
    public string Subject { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Details { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Pending";

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedUtc { get; set; }

    [MaxLength(2000)]
    public string Response { get; set; }
}
