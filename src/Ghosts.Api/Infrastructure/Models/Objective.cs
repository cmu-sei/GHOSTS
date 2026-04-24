using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ghosts.Api.Infrastructure.Models;

[Table("objectives")]
public class Objective
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public int? ScenarioId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = "MET"; // MET, JMET, Rehearsal, Onboarding, ToolTraining
    public string Status { get; set; } = "Draft"; // Draft, Active, Achieved, PartiallyMet, NotMet
    public string Score { get; set; } = "U"; // T (Trained), P (Practiced), U (Untrained)
    public int Priority { get; set; } = 1;
    public string SuccessCriteria { get; set; } = string.Empty;
    public string Assigned { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Objective Parent { get; set; }
    public Scenario Scenario { get; set; }
    public ICollection<Objective> Children { get; set; } = new List<Objective>();
}

// DTOs
public record ObjectiveDto(
    int Id,
    int? ParentId,
    int? ScenarioId,
    string Name,
    string Description,
    string Type,
    string Status,
    string Score,
    int Priority,
    string SuccessCriteria,
    string Assigned,
    int SortOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<ObjectiveDto> Children
);

public record CreateObjectiveDto(
    int? ParentId,
    int? ScenarioId,
    string Name,
    string Description,
    string Type,
    int Priority,
    string SuccessCriteria,
    string Assigned
);

public record UpdateObjectiveDto(
    string Name,
    string Description,
    string Type,
    string Status,
    string Score,
    int Priority,
    string SuccessCriteria,
    string Assigned,
    int SortOrder
);
