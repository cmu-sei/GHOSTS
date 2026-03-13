using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ghosts.Pandora.Infrastructure.Models;

[Table("direct_messages")]
public class DirectMessage
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public Guid FromUserId { get; set; }

    [Required]
    public Guid ToUserId { get; set; }


    [Required]
    [MaxLength(2000)]
    public string Message { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadUtc { get; set; }

    public virtual User FromUser { get; set; }
    public virtual User ToUser { get; set; }

    [NotMapped]
    public string FromUsername => FromUser?.Username ?? string.Empty;

    [NotMapped]
    public string ToUsername => ToUser?.Username ?? string.Empty;
}
