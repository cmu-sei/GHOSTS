using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ghosts.Pandora.Infrastructure.Models;

[Table("users")]
public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    public string Username { get; set; }

    [MaxLength(500)]
    public string Bio { get; set; }

    [MaxLength(255)]
    public string Avatar { get; set; }

    [MaxLength(50)]
    public string Status { get; set; }

    [MaxLength(50)]
    public string Theme { get; set; } = "default";

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastActiveUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();
    public virtual ICollection<Like> Likes { get; set; } = new List<Like>();
    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public virtual ICollection<DirectMessage> SentMessages { get; set; } = new List<DirectMessage>();
    public virtual ICollection<DirectMessage> ReceivedMessages { get; set; } = new List<DirectMessage>();
    public virtual ICollection<Followers> Following { get; set; } = new List<Followers>();
    public virtual ICollection<Followers> Followers { get; set; } = new List<Followers>();
}
