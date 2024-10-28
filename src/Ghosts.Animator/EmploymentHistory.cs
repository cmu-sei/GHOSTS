// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ghosts.Animator.Extensions;
using Ghosts.Animator.Models;
using Newtonsoft.Json;

namespace Ghosts.Animator
{
    public static class EmploymentHistory
    {
        public static EmploymentProfile GetEmployment()
        {
            var o = new EmploymentProfile();

            var numberOfJobs = AnimatorRandom.Rand.Next(0, 8);

            for (var i = 0; i < numberOfJobs; i++)
            {
                var employmentStatus = EmploymentProfile.EmploymentRecord.EmploymentStatuses.Resigned;

                var startDate = AnimatorRandom.Date();
                DateTime? endDate = startDate.AddDays(AnimatorRandom.Rand.Next(180, 2000));
                if (i == 0)
                {
                    endDate = null;
                    employmentStatus = EmploymentProfile.EmploymentRecord.EmploymentStatuses.FullTime;
                }

                var raw = File.ReadAllText("config/employment_jobtitles.json");
                var data = JsonConvert.DeserializeObject<DepartmentManager>(raw);

                var u = data.Departments.Sum(x => x.Probability);
                var r = AnimatorRandom.Rand.NextDouble() * u;
                double sum = 0;
                var assignedDepartment = data.Departments.FirstOrDefault(x => r <= (sum += x.Probability));

                u = assignedDepartment.Roles.Sum(x => x.Probability);
                r = AnimatorRandom.Rand.NextDouble() * u;
                sum = 0;
                var assignedRole = assignedDepartment.Roles.FirstOrDefault(x => r <= (sum += x.Probability));

                var job = new EmploymentProfile.EmploymentRecord
                {
                    Company = CompanyName(),
                    Department = assignedDepartment.Department,
                    JobTitle = assignedRole.Title
                };
                job.Email = $"{Npc.NpcProfile.Name.ToString().ToAccountSafeString()}@{job.Company.ToAccountSafeString()}.com".Replace("..", ".");
                //job.Manager
                job.Organization = job.Company;
                job.Phone = $"{PhoneNumber.GetPhoneNumber()} x####".Numerify();
                job.Salary = AnimatorRandom.Rand.Next(assignedRole.SalaryLow, assignedRole.SalaryHigh);
                job.StartDate = AnimatorRandom.Date();
                job.EndDate = endDate;
                job.EmploymentStatus = employmentStatus;
                job.Level = assignedRole.Level;

                job.Address = Address.GetHomeAddress();
                job.Address.Name = job.Company;

                job.Address.AddressType = "Employment";
                o.EmploymentRecords.Add(job);
            }

            return o;
        }

        public static string CompanyName()
        {
            return COMPANIES.RandomElement();
        }

        //private static readonly string[] DEPARTMENTS = { "Human Resources", "IT", "Logistics", "Finance", "Corporate Strategy", ""};

        private static readonly string[] COMPANIES =
        {
            "Cyberdyne Systems Corp.", "CHOAM", "Acme Corp.", "Sirius Cybernetics Corp.", "MomCorp", "Rich Industries", "Soylent Corp.",
            "Very Big Corp. of America", "Frobozz Magic Co.", "Warbucks Industries", "Tyrell Corp.", "Wayne Enterprises", "Virtucon",
            "Globex", "Umbrella Corp.", "Wonka Industries", "Stark Industries", "Clampett Oil", "Oceanic Airlines", "Yoyodyne Propulsion Sys.",
            "Gringotts", "Oscorp", "Nakatomi Trading Corp.", "Spacely Space Sprockets"
        };
    }

    public class Role
    {
        public string Title { get; set; }
        public double Probability { get; set; }
        public int SalaryHigh { get; set; }
        public int SalaryLow { get; set; }
        public int Level { get; set; }
    }

    public class DepartmentData
    {
        public string Department { get; set; }
        public double Probability { get; set; }
        public IList<Role> Roles { get; set; }
    }

    public class DepartmentManager
    {
        public IList<DepartmentData> Departments { get; set; }
    }
}
