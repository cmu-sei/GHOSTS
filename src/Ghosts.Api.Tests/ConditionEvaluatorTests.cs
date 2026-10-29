// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Api.Infrastructure.Services;

namespace Ghosts.Api.Tests;

/// <summary>
/// The schema's condition grammar, evaluated at run time the way experimental/rpg's engine does: terms joined by
/// &amp;&amp;, each flag:x, !x, objective:N or clock&gt;=minutes. An empty condition holds; a term it does not
/// know does not.
/// </summary>
public class ConditionEvaluatorTests
{
    private static readonly ExecutionRunState State = new(
        Flags: ["coffee-ready", "printer-low"],
        Facts: [],
        ObjectivesMet: new Dictionary<int, DateTime> { [3] = DateTime.UtcNow },
        Decisions: [],
        Responses: []);

    [Theory]
    [InlineData("", 0, true)]
    [InlineData(null, 0, true)]
    [InlineData("flag:coffee-ready", 0, true)]
    [InlineData("flag:lunch-ordered", 0, false)]
    [InlineData("!lunch-ordered", 0, true)]
    [InlineData("!coffee-ready", 0, false)]
    [InlineData("objective:3", 0, true)]
    [InlineData("objective:4", 0, false)]
    [InlineData("clock>=30", 30, true)]
    [InlineData("clock>=30", 29.9, false)]
    [InlineData("flag:coffee-ready && !lunch-ordered", 0, true)]
    [InlineData("flag:coffee-ready && flag:lunch-ordered", 0, false)]
    [InlineData("  flag:printer-low &&   clock>=10 ", 12, true)]
    [InlineData("weather:sunny", 0, false)]
    [InlineData("objective:x", 0, false)]
    public void A_condition_holds_by_the_grammars_rules(string? condition, double clockMinutes, bool holds)
    {
        Assert.Equal(holds, ConditionEvaluator.Holds(condition, State, clockMinutes));
    }
}
