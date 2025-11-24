using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ghosts.Pandora.Infrastructure.Models;

[Table("followers")]
public class Followers
{
    [Required]
    [MaxLength(50)]
    public string Username { get; set; }

    [Required]
    [MaxLength(50)]
    public string FollowerUsername { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public virtual User Follower { get; set; }
    public virtual User Followee { get; set; }
}
