using Ghosts.Animator;
using Ghosts.Animator.Models;

namespace Ghosts.Api.Tests;

public class NpcGenerationTests
{
    /// <summary>
    /// Every creation path in the api funnels through Npc.Generate, so anything left unset here
    /// shows up as a blank section on the npc detail screen and as missing context in chat prompts.
    /// Name prefix/middle/suffix, medical conditions and foreign travel are deliberately not
    /// asserted - those are meant to vary from npc to npc.
    /// </summary>
    [Fact]
    public void GenerateFillsEveryProfileSection()
    {
        for (var i = 0; i < 10; i++)
        {
            var p = Npc.Generate(MilitaryUnits.GetServiceBranch());

            Assert.False(string.IsNullOrEmpty(p.Name?.First));
            Assert.False(string.IsNullOrEmpty(p.Name?.Last));
            Assert.NotEmpty(p.Address);
            Assert.False(string.IsNullOrEmpty(p.Email));
            Assert.False(string.IsNullOrEmpty(p.Password));
            Assert.False(string.IsNullOrEmpty(p.CellPhone));
            Assert.False(string.IsNullOrEmpty(p.HomePhone));
            Assert.False(string.IsNullOrEmpty(p.CAC));
            Assert.False(string.IsNullOrEmpty(p.PhotoLink));
            Assert.NotEqual(default, p.Birthdate);

            Assert.NotEmpty(p.Preferences);
            Assert.NotEmpty(p.Attributes);
            Assert.NotEmpty(p.Education.Degrees);
            Assert.NotEmpty(p.Employment.EmploymentRecords);
            Assert.NotEmpty(p.Family.Members);
            Assert.NotEmpty(p.Finances.CreditCards);
            Assert.NotEmpty(p.Career.Strengths);
            Assert.NotEmpty(p.Career.Weaknesses);
            Assert.NotEmpty(p.Accounts);
            Assert.NotEmpty(p.Unit.Sub);

            Assert.False(string.IsNullOrEmpty(p.Rank?.Name));
            Assert.False(string.IsNullOrEmpty(p.Rank?.Pay));
            Assert.False(string.IsNullOrEmpty(p.Workstation?.Name));
            Assert.False(string.IsNullOrEmpty(p.Workstation?.IPAddress));
            Assert.True(p.Health.Height > 0);
            Assert.True(p.MentalHealth.IQ > 0);
            Assert.NotNull(p.InsiderThreat.Access);
            Assert.NotEqual(0, p.MotivationalProfile.Honor);

            // the first employment record is the npc's current job
            Assert.Null(p.Employment.EmploymentRecords[0].EndDate);
        }
    }
}
