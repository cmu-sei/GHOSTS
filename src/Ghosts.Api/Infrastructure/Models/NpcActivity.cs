using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ghosts.api.Infrastructure.Models;

[Table("npc_activity")]
public class NpcActivity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public Guid NpcId { get; set; }
    public ActivityTypes ActivityType { get; set; }
    public string Detail { get; set; }
    public DateTime CreatedUtc { get; set; }

    public enum ActivityTypes
    {
        SocialMediaPost = 0,
        NextAction = 10
    }
}
