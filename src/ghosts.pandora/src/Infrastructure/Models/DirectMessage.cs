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
    [MaxLength(50)]
    public string FromUsername { get; set; }

    [Required]
    [MaxLength(50)]
    public string ToUsername { get; set; }


    [Required]
    [MaxLength(2000)]
    public string Message { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadUtc { get; set; }

    public virtual User FromUser { get; set; }
    public virtual User ToUser { get; set; }
}
