// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;

namespace Ghosts.Animator.Models
{
    public class AccountsProfile
    {
        public int Id { get; set; }
        public IEnumerable<Account> Accounts { get; set; }

        public AccountsProfile()
        {
            Accounts = new List<Account>();
        }

        public class Account
        {
            public int Id { get; set; }
            public string Url { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
        }
    }
}
