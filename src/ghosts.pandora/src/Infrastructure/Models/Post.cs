using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ghosts.Pandora.Infrastructure.Models;

[Table("posts")]
public class Post
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string Username { get; set; }

    [Required]
    [MaxLength(5000)]
    public string Message { get; set; }

    [MaxLength(50)]
    public string Theme { get; set; } = "default";

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedUtc { get; set; } = DateTime.UtcNow;

    public virtual User User { get; set; }
    public virtual ICollection<Like> Likes { get; set; } = new List<Like>();
    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
}
