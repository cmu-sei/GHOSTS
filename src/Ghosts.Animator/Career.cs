// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using System.Linq;
using Ghosts.Animator.Extensions;
using Ghosts.Animator.Models;

namespace Ghosts.Animator
{
    public static class Career
    {
        public static IEnumerable<CareerProfile.StrengthProfile> GetStrengths()
        {
            return GetList(STRENGTHS).Select(o => new CareerProfile.StrengthProfile { Name = o }).ToList();
        }

        public static CareerProfile.StrengthProfile GetStrength()
        {
            return new CareerProfile.StrengthProfile { Name = STRENGTHS.RandomElement() };
        }

        public static IEnumerable<CareerProfile.WeaknessProfile> GetWeaknesses()
        {
            return GetList(WEAKNESSES).Select(o => new CareerProfile.WeaknessProfile { Name = o }).ToList();
        }

        public static CareerProfile.WeaknessProfile GetWeakness()
        {
            return new CareerProfile.WeaknessProfile { Name = WEAKNESSES.RandomElement() };
        }

        public static int GetWorkEthic()
        {
            return new Dictionary<string, double>
            {
                {"-50|-10", 10},
                {"-9|10", 10},
                {"11|30", 20},
                {"31|50", 40},
                {"51|60", 10},
                {"61|80", 10}
            }.RandomFromPipedProbabilityList();
        }

        public static int GetTeamValue()
        {
            return new Dictionary<string, double>
            {
                {"-50|-10", 8},
                {"-9|10", 10},
                {"11|30", 20},
                {"31|50", 40},
                {"51|60", 10},
                {"61|80", 10},
                {"81|90", 2}
            }.RandomFromPipedProbabilityList();
        }

        private static IEnumerable<string> GetList(string[] options)
        {
            var list = new List<string>();
            for (var i = 0; i < AnimatorRandom.Rand.Next(0, 12); i++)
            {
                list.Add(options.RandomElement());
            }

            return list;
        }

        private static readonly string[] STRENGTHS =
        {
            "Facilitator", "Documentation Specialist", "Active Listener", "Adaptable", "Good Communicator", "Creative", "Critical Thinker",
            "Customer Focused", "Decision Maker", "Interpersonal Communicator", "Leader", "Organizer", "Public Speaker", "Problem-solver",
            "Team Player", "Data Entry", "Answering Phones", "Billing", "Scheduling", "MS Office", "Office Equipment", "QuickBooks", "Shipping",
            "Welcoming Visitors", "Calendar Management", "Sales", "Product Knowledge", "Lead Qualification", "Lead Prospecting ",
            "Customer Needs Analysis", "Referral Marketing", "Contract Negotiation", "Self Motivation", "Increasing Customer Lifetime Value (CLV)",
            "Reducing Customer Acquisition Cost (CAC)", "CRM Software", "POS Skills", "Cashier Skills", "Programming Languages", "Web Development",
            "Data Structures", "Open Source Experience", "CodingJava Script", "Security", "Machine Learning", "Debugging", "UX/UI",
            "Front-End & Back-End Development", "Cloud Management", "Agile Development", "STEM Skills", "CAD", "Design", "Prototyping", "Testing",
            "Troubleshooting", "Project Launch", "Lean Manufacturing", "Workflow Development", "Computer Skills", "SolidWorks", "Budgeting",
            "Technical Report Writing", "SEO/SEM", "PPC", "CRO", "A/B Testing", "Social Media Marketing and Paid Social Media Advertising",
            "Sales Funnel Management", "CMS Tools", "Graphic Design Skills", "Email Marketing", "Email Automation", "Data Visualization", "CPC",
            "Typography", "Print Design", "Photography and Branding", "Agile", "Managing Cross-Functional Teams", "Scrum", "Performance Tracking",
            "Financial Modelling", "Ideas Leadership", "Feature Definition", "Forecasting", "Profit and Loss", "Scope Management",
            "Project Lifecycle Management", "Meeting Facilitation"
        };

        private static readonly string[] WEAKNESSES =
        {
            "Confrontation", "Covering for co-workers", "Expecting too much from colleagues",
            "Expressing too much frustration with under performing staff or colleagues", "Presenting to large groups", "Public speaking",
            "Being too critical of other people’s work", "Too easily internalizing the problems of clients", "Being too sensitive", "Creativity",
            "Delegating tasks", "Humor", "Spontaneity (you work better when prepared)", "Organization", "Patience", "Taking too many risks",
            "Being too honest", "Standardized tests", "Leaving projects unfinished", "Providing too much detail in reports",
            "Shifting from one project to another (multitasking)", "Taking credit for group projects", "Taking on too many projects at once",
            "Taking on too much responsibility", "Being too detail-oriented", "Being too much of a perfectionist", "Procrastination",
            "Being too helpful to others", "Working too many hours", "Cupcakes", "Donuts"
        };
    }
}
