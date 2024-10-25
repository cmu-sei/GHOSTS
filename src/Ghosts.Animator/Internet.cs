// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Ghosts.Animator.Extensions;
using Ghosts.Animator.Models;

namespace Ghosts.Animator
{
    public static class Internet
    {
        static Internet()
        {
            BYTE = 0.To(255).Select(item => item.ToString()).ToArray();
        }

        public static string GetEmail(string name = null)
        {
            return GetUserName(name) + '@' + GetDomainName();
        }

        public static string GetMilEmail(string name = null, string affiliation = "mil")
        {
            var o = $"{GetMilUserName(name)}.{affiliation}";
            o = Regex.Replace(o, @"[^0-9a-zA-Z\._]", "");
            o += "@mail.mil";
            return o;
        }

        /// <summary>
        /// Returns an email address of an online disposable email service (like tempinbox.com).
        /// you can really send an email to these addresses an access it by going to the service web pages.
        /// <param name="name">User Name initial value.</param>
        /// </summary>
        public static string GetDisposableEmail(string name = null)
        {
            return GetUserName(name) + '@' + DISPOSABLE_HOSTS.RandomElement();
        }

        public static string GetFreeEmail(string name = null)
        {
            return GetUserName(name) + "@" + HOSTS.RandomElement();
        }

        public static string GetUserName(string name = null)
        {
            // 67% chance to get a random username from the file
            if (AnimatorRandom.Rand.Next(0, 3) > 1)
            {
                var file = "config/usernames.txt";
                return file.GetRandomFromFile();
            }

            // If name is null, generate a new name
            if (name == null)
            {
                switch (AnimatorRandom.Rand.Next(2))
                {
                    case 0:
                        name = new Regex(@"\W").Replace(Name.GetFirstName(), "").ToLower();
                        break;
                    default:
                        name = new[]
                        {
                            new Regex(@"\W").Replace(Name.GetFirstName(), ""),
                            new Regex(@"\W").Replace(Name.GetLastName(), "")
                        }.Join(new[] { ".", "_" }.RandomElement()).ToLower();
                        break;
                }
            }

            // Convert the name to an account-safe string
            name = name.ToAccountSafeString();

            // Split and join the name using a random delimiter if there are spaces
            name = name.Split(' ').Join(new[] { ".", "_" }.RandomElement()).ToLower();

            // Randomly shorten the name
            if (AnimatorRandom.Rand.Next(0, 4) > 0)
            {
                if (name.Length > 1)
                {
                    name = name.Substring(0, AnimatorRandom.Rand.Next(1, name.Length));
                }
            }

            // Randomly append a number
            if (AnimatorRandom.Rand.Next(0, 4) > 0)
            {
                name += AnimatorRandom.Rand.Next(0, 10000); // Range 0 to 9999
            }

            return name;
        }

        public static string GetPassword(int length = 8)
        {
            const string valid = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*()_+|}{[]<>?,./;:";
            var res = new StringBuilder();
            while (0 < length--)
                res.Append(valid[AnimatorRandom.Rand.Next(valid.Length)]);
            return res.ToString();
        }

        public static string GetComputerName()
        {
            return "flag-##.local".Numerify();
        }

        public static string GetMilUserName(string name = null)
        {
            if (name != null)
            {
                var parts = name.Split(' ').Join(new[] { "." }.RandomElement());
                return parts.ToLower();
            }

            switch (AnimatorRandom.Rand.Next(2))
            {
                case 0:
                    return new Regex(@"\W").Replace(Name.GetFirstName(), "").ToLower();
                case 1:
                    var parts = new[] { Name.GetFirstName(), Name.GetLastName() }.Select(n => new Regex(@"\W").Replace(n, ""));
                    return parts.Join(new[] { "." }.RandomElement()).ToLower();
                default: throw new ApplicationException();
            }
        }

        public static string GetDomainName()
        {
            return GetDomainWord() + "." + GetDomainSuffix();
        }

        public static string GetMilDomainName()
        {
            return GetDomainWord() + ".mil";
        }

        public static string GetDomainWord()
        {
            var dw = Company.GetName().Split(' ').First();
            dw = new Regex(@"\W").Replace(dw, "");
            dw = dw.ToLower();
            return dw;
        }

        public static string GetDomainSuffix()
        {
            return DOMAIN_SUFFIXES.RandomElement();
        }

        public static string GetUri(string protocol)
        {
            return protocol + "://" + GetDomainName();
        }

        public static string GetHttpUrl()
        {
            return GetUri("http");
        }

        public static string GetIP_V4_Address()
        {
            return BYTE.RandPick(4).Join(".");
        }

        public static AccountsProfile GetAccountProfile(string name = null)
        {
            var o = new AccountsProfile { Accounts = GetAccounts(name) };
            return o;
        }

        public static IEnumerable<AccountsProfile.Account> GetAccounts(string name)
        {
            if (string.IsNullOrEmpty(name))
                return new List<AccountsProfile.Account>();

            var o = new List<AccountsProfile.Account>();

            var numberOfAccounts = AnimatorRandom.Rand.Next(0, 15);

            for (var i = 0; i < numberOfAccounts; i++)
            {
                o.Add(GetSocialAccount(name));
            }

            numberOfAccounts = AnimatorRandom.Rand.Next(0, 6);
            for (var i = 0; i < numberOfAccounts; i++)
            {
                o.Add(GetAccount(name));
            }

            return o;
        }

        public static AccountsProfile.Account GetAccount(string name = null)
        {
            var o = new AccountsProfile.Account { Username = GetUserName(name), Password = GetPassword(), Url = GetHttpUrl() };
            return o;
        }

        public static AccountsProfile.Account GetSocialAccount(string name = null)
        {
            var o = new AccountsProfile.Account { Username = GetUserName(name), Password = GetPassword(), Url = SOCIAL_ACCOUNTS.RandomElement() };
            return o;
        }

        private static readonly string[] BYTE; //new [] { ((0..255).to_a.map { |n| n.to_s })
        private static readonly string[] HOSTS = { "gmail.com", "yahoo.com", "outlook.com" };

        private static readonly string[] DISPOSABLE_HOSTS =
            {"mailinator.com", "suremail.info", "spamherelots.com", "binkmail.com", "safetymail.info", "tempinbox.com"};

        private static readonly string[] DOMAIN_SUFFIXES = { "co.uk", "com", "us", "uk", "ca", "biz", "info", "name" };

        private static readonly string[] SOCIAL_ACCOUNTS =
        {
            "gmail", "openvpn", "nmail", "Facebook", "Instagram", "Snapchat", "Signal", "WhatsApp", "Twitter", "Youtube", "QQ", "Tumblr", "TikTok",
            "Reddit", "LinkedIn", "Pinterest", "Telegram", "Medium", "GitHub", "Google Hangouts"
        };
    }
}
