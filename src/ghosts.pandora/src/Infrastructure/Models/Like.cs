using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ghosts.Pandora.Infrastructure.Models;

[Table("likes")]
public class Like
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public string Username { get; set; }

    [Required]
    public Guid PostId { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public virtual User User { get; set; }
    public virtual Post Post { get; set; }
}
