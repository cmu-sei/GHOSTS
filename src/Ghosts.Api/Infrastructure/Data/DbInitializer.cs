// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Ghosts.Api.Infrastructure.Data
{
    public class DbInitializer
    {
        public static async Task Initialize(ApplicationDbContext context, ILogger<DbInitializer> logger)
        {
            context.Database.EnsureCreated();

            // Could do additional seeding here in the future
            //if (context.Machines.Any()) return; // DB has been seeded
        }
    }
}