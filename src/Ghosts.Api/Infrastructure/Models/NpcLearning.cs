// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Ghosts.Api.Infrastructure.Models;

[Table("npclearning")]
public class NpcLearning
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int Id { get; set; }

    [Required]
    public Guid NpcId { get; set; }

    [Required]
    public Guid ToNpcId { get; set; }

    [Required]
    public Guid FromNpcId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Topic { get; set; }

    public long Step { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int Value { get; set; }

    public DateTime CreatedUtc { get; set; }

    // Navigation property
    [ForeignKey("NpcId")]
    public virtual NpcRecord Npc { get; set; }

    public NpcLearning()
    {
        CreatedUtc = DateTime.UtcNow;
    }

    public NpcLearning(Guid npcId, Guid toNpcId, Guid fromNpcId, string topic, long step, int value)
    {
        NpcId = npcId;
        ToNpcId = toNpcId;
        FromNpcId = fromNpcId;
        Topic = topic;
        Step = step;
        Value = value;
        CreatedUtc = DateTime.UtcNow;
    }

    public override string ToString()
    {
        return $"{ToNpcId},{FromNpcId},{Topic},{Step},{Value}";
    }
}
