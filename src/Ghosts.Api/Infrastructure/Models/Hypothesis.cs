// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ghosts.Api.Infrastructure.Models;

[Table("hypotheses")]
public class Hypothesis
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Keywords { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,6)")]
    public decimal DefaultLikelihood { get; set; } = 0.6m;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
