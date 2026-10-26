// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Api.Infrastructure.Services;

namespace Ghosts.Api.Tests;

/// <summary>
/// The executor schedules every timeline item from its offset. The schema allows days, hours and minutes
/// ("T+2d9h40m"), and the validator accepts them, so the executor must read them the same way. An offset it
/// cannot read is 0, which fires the item at the start of the run.
/// </summary>
public class ExecutionTimeOffsetTests
{
    [Theory]
    [InlineData("T+0m", 0)]
    [InlineData("T+15m", 15)]
    [InlineData("T+130m", 130)]
    [InlineData("T+2h", 120)]
    [InlineData("T+1h40m", 100)]
    [InlineData("T+1d", 1440)]
    [InlineData("T+2d9h40m", 3460)]
    public void Offsets_in_the_schema_form_are_read_in_full(string time, int minutes)
    {
        Assert.Equal(minutes * 60_000, ExecutionService.ParseTimeOffsetMs(time));
    }

    [Theory]
    [InlineData("t+15m", 15)]
    [InlineData(" T+15m ", 15)]
    public void Minute_offsets_that_parsed_before_still_parse(string time, int minutes)
    {
        Assert.Equal(minutes * 60_000, ExecutionService.ParseTimeOffsetMs(time));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Day 2, morning")]
    public void Anything_else_is_zero(string? time)
    {
        Assert.Equal(0, ExecutionService.ParseTimeOffsetMs(time));
    }
}
