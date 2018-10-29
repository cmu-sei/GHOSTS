// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Linq;
using System.Threading.Tasks;
using Ghosts.Api.Code;
using Ghosts.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Ghosts.Api.Data
{
    public class DbInitializer
    {
        public static async Task Initialize(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager, ILogger<DbInitializer> logger)
        {
            context.Database.EnsureCreated();

            // Look for any users.
            if (context.Users.Any())
            {
                return; // DB has been seeded
            }

            await CreateDefaultUserAndRoleForApplication(userManager, roleManager, logger);
            var m1 = new Machine
            {
                Name = "test1",
                FQDN = "test1.fqdn.variable.tld",
                HostIp = "0.0.0.0",
                IPAddress = "192.168.0.11",
                CurrentUsername = "testuser1"
            };

            var m2 = new Machine
            {
                Name = "test2",
                FQDN = "test2.fqdn.variable.tld",
                HostIp = "1.1.1.1",
                IPAddress = "192.168.0.12",
                CurrentUsername = "testuser2"
            };

            var m3 = new Machine
            {
                Name = "test3",
                FQDN = "test3.fqdn.variable.tld",
                HostIp = "2.2.2.2",
                IPAddress = "192.168.0.13",
                CurrentUsername = "testuser3"
            };

            context.Machines.Add(m1);
            context.Machines.Add(m2);
            context.Machines.Add(m3);
            context.SaveChanges();

            var mg1 = new Group();
            mg1.Name = "Test Group Carnegie";

            mg1.GroupMachines.Add(new GroupMachine{MachineId = m1.Id});
            mg1.GroupMachines.Add(new GroupMachine { MachineId = m2.Id });

            context.Groups.Add(mg1);

            var mg2 = new Group();
            mg2.Name = "Test Group Mellon";
            mg2.Machines.Add(m3);

            mg2.GroupMachines.Add(new GroupMachine { MachineId = m3.Id });

            context.Groups.Add(mg2);
            context.SaveChanges();

            foreach (var role in Enum.GetValues(typeof(ApiDetails.Roles)))
            {
                if (!roleManager.RoleExistsAsync(role.ToString()).Result)
                    await roleManager.CreateAsync(new IdentityRole(role.ToString()));
            }
            
            var adminUser = new ApplicationUser { UserName = Program.InitConfig.AdminUsername, Email = Program.InitConfig.AdminUsername,
                Created = DateTime.UtcNow, Id = Guid.NewGuid().ToString() };
            await userManager.CreateAsync(adminUser, Program.InitConfig.AdminPassword);
            await userManager.AddToRoleAsync(adminUser, ApiDetails.Roles.Admin.ToString());
        }

        private static async Task CreateDefaultUserAndRoleForApplication(UserManager<ApplicationUser> um, RoleManager<IdentityRole> rm, ILogger<DbInitializer> logger)
        {
            var administratorRole = ApiDetails.Roles.Admin.ToString();
            var email = Program.InitConfig.AdminUsername;

            await CreateDefaultAdministratorRole(rm, logger, administratorRole);
            var user = await CreateDefaultUser(um, logger, email);
            await SetPasswordForDefaultUser(um, logger, email, user);
            await AddDefaultRoleToDefaultUser(um, logger, email, administratorRole, user);
        }

        private static async Task CreateDefaultAdministratorRole(RoleManager<IdentityRole> rm, ILogger<DbInitializer> logger, string administratorRole)
        {
            logger.LogInformation($"Create the role `{administratorRole}` for application");
            var ir = await rm.CreateAsync(new IdentityRole(administratorRole));
            if (ir.Succeeded)
            {
                logger.LogDebug($"Created the role `{administratorRole}` successfully");
            }
            else
            {
                var exception = new ApplicationException($"Default role `{administratorRole}` cannot be created");
                logger.LogError(exception, GetIdentiryErrorsInCommaSeperatedList(ir));
                throw exception;
            }
        }

        private static async Task<ApplicationUser> CreateDefaultUser(UserManager<ApplicationUser> um, ILogger<DbInitializer> logger, string email)
        {
            logger.LogInformation($"Create default user with email `{email}` for application");
            var user = new ApplicationUser(email);

            var ir = await um.CreateAsync(user);
            if (ir.Succeeded)
            {
                logger.LogDebug($"Created default user `{email}` successfully");
            }
            else
            {
                var exception = new ApplicationException($"Default user `{email}` cannot be created");
                logger.LogError(exception, GetIdentiryErrorsInCommaSeperatedList(ir));
                throw exception;
            }

            var createdUser = await um.FindByEmailAsync(email);
            return createdUser;
        }

        private static async Task SetPasswordForDefaultUser(UserManager<ApplicationUser> um, ILogger<DbInitializer> logger, string email, ApplicationUser user)
        {
            logger.LogInformation($"Set password for default user `{email}`");
            var password = Program.InitConfig.AdminPassword;
            var ir = await um.AddPasswordAsync(user, password);
            if (ir.Succeeded)
            {
                logger.LogTrace($"Set password `{password}` for default user `{email}` successfully");
            }
            else
            {
                var exception = new ApplicationException($"Password for the user `{email}` cannot be set");
                logger.LogError(exception, GetIdentiryErrorsInCommaSeperatedList(ir));
                throw exception;
            }
        }

        private static async Task AddDefaultRoleToDefaultUser(UserManager<ApplicationUser> um, ILogger<DbInitializer> logger, string email, string administratorRole, ApplicationUser user)
        {
            logger.LogInformation($"Add default user `{email}` to role '{administratorRole}'");
            var ir = await um.AddToRoleAsync(user, administratorRole);
            if (ir.Succeeded)
            {
                logger.LogDebug($"Added the role '{administratorRole}' to default user `{email}` successfully");
            }
            else
            {
                var exception = new ApplicationException($"The role `{administratorRole}` cannot be set for the user `{email}`");
                logger.LogError(exception, GetIdentiryErrorsInCommaSeperatedList(ir));
                throw exception;
            }
        }

        private static string GetIdentiryErrorsInCommaSeperatedList(IdentityResult ir)
        {
            string errors = null;
            foreach (var identityError in ir.Errors)
            {
                errors += identityError.Description;
                errors += ", ";
            }
            return errors;
        }
    }
}
