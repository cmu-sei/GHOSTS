using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ghosts.Pandora.Infrastructure.Models;

[Table("followers")]
public class Followers
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    public Guid FollowerUserId { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public virtual User Follower { get; set; }
    public virtual User Followee { get; set; }

    [NotMapped]
    public string Username => Followee?.Username ?? string.Empty;

    [NotMapped]
    public string FollowerUsername => Follower?.Username ?? string.Empty;
}
