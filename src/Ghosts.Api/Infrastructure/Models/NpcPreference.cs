// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Ghosts.Api.Infrastructure.Models;

[Table("npcpreferences")]
[method: JsonConstructor]
public class NpcPreference(int id, Guid socialGraphId, Guid toNpcId, Guid fromNpcId, string name, long step, decimal weight, decimal strength)
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; } = id;

    [Required]
    public Guid SocialGraphId { get; set; } = socialGraphId;

    [Required]
    public Guid ToNpcId { get; set; } = toNpcId;

    [Required]
    public Guid FromNpcId { get; set; } = fromNpcId;

    /// <summary>
    /// Preference name/topic (e.g., "technology", "sports", "politics", "cybersecurity")
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Name { get; set; } = name;

    /// <summary>
    /// Simulation step when this preference was recorded
    /// </summary>
    public long Step { get; set; } = step;

    /// <summary>
    /// Base weight/importance of this preference (0.0 to 1.0)
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal Weight { get; set; } = weight;

    /// <summary>
    /// Current strength of preference, can increase/decrease over time based on exposure and interactions
    /// Similar to Bayesian posterior in beliefs
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal Strength { get; set; } = strength;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    // Navigation property
    [ForeignKey("SocialGraphId")]
    public virtual NpcSocialGraph SocialGraph { get; set; }

    public NpcPreference() : this(0, Guid.Empty, Guid.Empty, Guid.Empty, string.Empty, 0, 0, 0) { }

    public override string ToString()
    {
        return $"{ToNpcId},{FromNpcId},{Name},{Step},{Weight},{Strength}";
    }

    public static string ToHeader()
    {
        return "To,From,Name,Step,Weight,Strength";
    }
}
