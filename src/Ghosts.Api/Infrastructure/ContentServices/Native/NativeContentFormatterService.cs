// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Linq;
using System.Threading.Tasks;
using ghosts.api.Infrastructure.Models;
using Ghosts.Animator;
using Ghosts.Animator.Extensions;
using Ghosts.Animator.Models;
using NLog;

namespace ghosts.api.Infrastructure.ContentServices.Native;

public class NativeContentFormatterService : IFormatterService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    public async Task<string> GenerateNextAction(NpcRecord npc, string history)
    {
        //TODO
        return await Task.FromResult(string.Empty);
    }

    public async Task<string> ExecuteQuery(string prompt)
    {
        //TODO
        return await Task.FromResult(string.Empty);
    }

    public async Task<string> GenerateTweet(NpcRecord npc)
    {
        string tweetText;

        if (npc.NpcProfile.Birthdate.Date.DayOfYear == DateTime.Now.Date.DayOfYear)
        {
            tweetText = ProcessBirthday(npc.NpcProfile);
        }
        else
        {
            var i = AnimatorRandom.Rand.Next(0, 15);
            tweetText = i switch
            {
                0 => ProcessAddress(npc.NpcProfile),
                1 => ProcessFamily(npc.NpcProfile),
                2 => ProcessEmployment(npc.NpcProfile),
                3 => ProcessEducation(npc.NpcProfile),
                4 => ProcessAccount(npc.NpcProfile),
                _ => Faker.Lorem.Sentence() //default is just text, no personal information
            };
        }

        return await Task.FromResult(tweetText);
    }

    private static string ProcessAccount(NpcProfile agent)
    {
        try
        {
            if (!agent.Accounts.Any()) return "";
            {
                var o = agent.Accounts.RandomElement();
                var list = new[]
                {
                    $"Check out my new post on {o.Url}",
                    $"Check out my new picture uploaded to {o.Url}",
                    $"New post on {o.Url}",
                    $"Join my newsletter on {o.Url}"
                };
                return list.RandomFromStringArray();
            }
        }
        catch (Exception e)
        {
            _log.Error(e);
        }

        return Faker.Lorem.Sentence();
    }

    private static string ProcessBirthday(NpcProfile agent)
    {
        try
        {
            var list = new[]
            {
                "Happy birthday to me!",
                $"Out for dinner on my {DateTime.Now.Year - agent.Birthdate.Year} birthday!",
                $"I'm {DateTime.Now.Year - agent.Birthdate.Year} today!",
                $"{DateTime.Now.Year - agent.Birthdate.Year} looks good on me."
            };
            return list.RandomFromStringArray();
        }
        catch (Exception e)
        {
            _log.Error(e);
        }

        return Faker.Lorem.Sentence();
    }

    private static string ProcessFamily(NpcProfile agent)
    {
        try
        {
            if (!agent.Family.Members.Any()) return "";
            var o = agent.Family.Members.RandomElement();
            var list = new[]
            {
                $"Visiting my {o.Relationship} {o.Name} today.",
                $"Hanging with {o.Name} my {o.Relationship}.",
                $"{o.Relationship} and I say {o.Name} - ",
                $"My {o.Relationship} {o.Name}."
            };
            return $"{list.RandomFromStringArray()} {Faker.Lorem.Sentence()}";
        }
        catch (Exception e)
        {
            _log.Error(e);
        }
        return Faker.Lorem.Sentence();
    }

    private static string ProcessAddress(NpcProfile agent)
    {
        try
        {
            if (!agent.Address.Any()) return "";
            var o = agent.Address.RandomElement();
            var list = new[]
            {
                $"Visiting the {o.State} capital today. Beautiful!",
                $"Chilling in {o.City} today. Literally freezing.",
                $"Love {o.City} - so beautiful!",
                $"Love {o.City} - so great!"
            };
            return list.RandomFromStringArray();
        }
        catch (Exception e)
        {
            _log.Error(e);
        }
        return Faker.Lorem.Sentence();
    }

    private static string ProcessEmployment(NpcProfile agent)
    {
        try
        {
            if (!agent.Employment.EmploymentRecords.Any()) return "";
            var o = agent.Employment.EmploymentRecords.RandomElement();
            var list = new[]
            {
                $"Love working at {o.Company}",
                $"Hanging with my peeps from {o.Company} at the office today!",
                $"{o.Company} is hiring for my team - DM me for details",
                $"My team at {o.Company} is hiring WFH - DM me for info"
            };
            return list.RandomFromStringArray();
        }
        catch (Exception e)
        {
            _log.Error(e);
        }
        return Faker.Lorem.Sentence();
    }

    private static string ProcessEducation(NpcProfile agent)
    {
        try
        {
            if (agent.Education.Degrees.Count == 0) return "";
            var o = agent.Education.Degrees.RandomElement();
            var list = new[]
            {
                $"{o.School.Name} is the best school in the world!",
                $"On campus of {o.School.Name} - great to be back!",
                $"The {o.School.Name} campus is beautiful this time of year!",
                $"GO {o.School.Name}!!!"
            };
            return list.RandomFromStringArray();
        }
        catch (Exception e)
        {
            _log.Error(e);
        }
        return Faker.Lorem.Sentence();
    }
}
