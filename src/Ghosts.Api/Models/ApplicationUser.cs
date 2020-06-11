// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Ghosts.Api.Models
{
    public class ApplicationUser : IdentityUser
    {
        public ApplicationUser()
        {
            Created = DateTime.UtcNow;
        }

        public ApplicationUser(string email) : base(email)
        {
            base.Email = email;
            Created = DateTime.UtcNow;
        }

        [Required] [DataType(DataType.Date)] public DateTime Created { get; set; }
    }
}