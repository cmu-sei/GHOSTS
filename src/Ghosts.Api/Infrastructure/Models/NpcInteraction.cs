// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Ghosts.Api.Infrastructure.Models;

[Table("npcinteractions")]
public class NpcInteraction
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string SocialConnectionId { get; set; }

    public long Step { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int Value { get; set; }

    public DateTime CreatedUtc { get; set; }

    // Navigation property
    [ForeignKey("SocialConnectionId")]
    public virtual NpcSocialConnection SocialConnection { get; set; }

    public NpcInteraction()
    {
        CreatedUtc = DateTime.UtcNow;
    }
}
