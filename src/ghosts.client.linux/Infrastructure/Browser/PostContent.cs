using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileHelpers;
using Ghosts.Domain.Code;
using NLog;

namespace ghosts.client.linux.Infrastructure.Browser
{
    /// <summary>
    /// The classes in this file assist with auto-generation of POST content
    /// </summary>
    public class PostContentManager
    {

        private static readonly Random _random = new();

        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        private static readonly char[] seperators = { '.', '_' };

        public string Subject { private set; get; }
        public string Body { private set; get; }

        public string FullName { private set; get; }

        public string Email { private set; get; }


        internal IList<GenericPostContent> GenericContent { private set; get; }
        internal IList<FirstName> firstNames { private set; get; }
        internal IList<LastName> lastNames { private set; get; }
        internal IList<EmailTarget> emailTargets { private set; get; }

        public PostContentManager()
        {
            LoadAllContent();
        }

        private static string getRandomInitial(bool uppercase)
        {
            var c = (char)_random.Next((int)'a', ((int)'z') + 1);
            if (uppercase) return c.ToString().ToUpper();
            else return c.ToString();
        }

        //Capitalize first letter
        private static string getCapitializeFirst(bool uppercase, string s)
        {
            if (uppercase)
            {
                string retval;
                if (s.Contains('-'))
                {
                    //handle conjoined last name, capitalize first letter of each name
                    var words = s.Split('-');
                    retval = words[0][0].ToString().ToUpper() + words[0].Substring(1) + "-" + words[1][0].ToString().ToUpper() + words[1].Substring(1);
                }
                else
                {
                    retval = string.Concat(s[0].ToString().ToUpper(), s.AsSpan(1));
                }
                return retval;
            }
            else return s;
        }

        private string getRandomFirstName()
        {
            if (firstNames.Count > 0) return firstNames[_random.Next(0, firstNames.Count)].value.Trim().ToLower();
            else return "nofirstnameavailable";
        }
        private string getRandomLastName()
        {
            if (lastNames.Count > 0) return lastNames[_random.Next(0, lastNames.Count)].value.Trim().ToLower();
            else return "nolastnameavailable";
        }

        private string getRandomEmailTarget()
        {
            if (emailTargets.Count > 0) return emailTargets[_random.Next(0, emailTargets.Count)].value.Trim().ToLower();
            else return "noemailtargetavailable";
        }



        /// <summary>
        /// This generates a FullName, and matching email.
        /// The matching email is a variation of the fullname in different ways
        /// such lastname.first name or firstname_lastname or other combinations
        /// </summary>
        /// <returns></returns>
        public void NameEmailNext()
        {
            var firstName = getRandomFirstName();
            var lastName = getRandomLastName();
            var lastName2 = getRandomLastName();
            var emailTarget = getRandomEmailTarget();
            var seperator = seperators[_random.Next(0, seperators.Length)].ToString();
            if (_random.Next(0, 10) == 0) lastName = lastName + "-" + lastName2;  //10% of names are conjoined
            var useInitials = _random.Next(0, 2) > 0;
            var useFirstNameFirst = _random.Next(0, 2) > 0;
            var useMiddleInitial = _random.Next(0, 2) > 0;
            var firstInitial = firstName[0].ToString();
            var middleInitial = getRandomInitial(false);
            var useUpperCase = _random.Next(0, 2) > 0;

            if (useMiddleInitial) FullName = $"{getCapitializeFirst(true, firstName)} {middleInitial.ToUpper()}. {getCapitializeFirst(true, lastName)}";
            else FullName = $"{getCapitializeFirst(true, firstName)} {getCapitializeFirst(true, lastName)}";

            string name;  //this is the email name
            if (useInitials)
            {
                if (useMiddleInitial) name = $"{getCapitializeFirst(useUpperCase, firstInitial)}{seperator}{getCapitializeFirst(useUpperCase, middleInitial)}{seperator}{getCapitializeFirst(useUpperCase, lastName)}";
                else name = $"{getCapitializeFirst(useUpperCase, firstInitial)}{seperator}{getCapitializeFirst(useUpperCase, lastName)}";
            }
            else if (useFirstNameFirst)
            {
                if (useMiddleInitial) name = $"{getCapitializeFirst(useUpperCase, firstName)}{seperator}{getCapitializeFirst(useUpperCase, middleInitial)}{seperator}{getCapitializeFirst(useUpperCase, lastName)}";
                else name = $"{getCapitializeFirst(useUpperCase, firstName)}{seperator}{getCapitializeFirst(useUpperCase, lastName)}";
            }
            else
            {
                if (useMiddleInitial) name = $"{getCapitializeFirst(useUpperCase, lastName)}{seperator}{getCapitializeFirst(useUpperCase, firstName)}{seperator}{getCapitializeFirst(useUpperCase, middleInitial)}";
                else name = $"{getCapitializeFirst(useUpperCase, lastName)}{seperator}{getCapitializeFirst(useUpperCase, firstName)}";
            }

            Email = $"{name}@{emailTarget}";

        }



        public void GenericContentNext()
        {

            var total = GenericContent.Count;

            if (total <= 0)
            {
                Subject = "nogenericcontentavailable";
                Body = "nogenericcontentavailable";
                return;
            };


            var o = GenericContent[_random.Next(0, total)];


            Subject = o.Subject.Replace("\\n", "\n");
            Body = o.Body.Replace("\\n", "\n");
        }


        public void LoadAllContent()
        {
            try
            {
                var engine = new FileHelperEngine<GenericPostContent>
                {
                    Encoding = Encoding.UTF8
                };
                GenericContent = engine.ReadFile(ClientConfigurationResolver.GenericPostContent).ToList();
            }
            catch (Exception e)
            {
                _log.Error($"Post Generic content file {ClientConfigurationResolver.GenericPostContent} could not be loaded: {e}");
                GenericContent = new List<GenericPostContent>();
            }
            try
            {
                var engine = new FileHelperEngine<FirstName>
                {
                    Encoding = Encoding.UTF8
                };
                firstNames = engine.ReadFile(ClientConfigurationResolver.FirstNames).ToList();
            }
            catch (Exception e)
            {
                _log.Error($"First Name content file {ClientConfigurationResolver.FirstNames} could not be loaded: {e}");
                firstNames = new List<FirstName>();
            }
            try
            {
                var engine = new FileHelperEngine<LastName>
                {
                    Encoding = Encoding.UTF8
                };
                lastNames = engine.ReadFile(ClientConfigurationResolver.LastNames).ToList();
            }
            catch (Exception e)
            {
                _log.Error($"Last Name content file {ClientConfigurationResolver.LastNames} could not be loaded: {e}");
                lastNames = new List<LastName>();
            }
            try
            {
                var engine = new FileHelperEngine<EmailTarget>
                {
                    Encoding = Encoding.UTF8
                };
                emailTargets = engine.ReadFile(ClientConfigurationResolver.EmailTargets).ToList();
            }
            catch (Exception e)
            {
                _log.Error($"Last Name content file {ClientConfigurationResolver.EmailTargets} could not be loaded: {e}");
                emailTargets = new List<EmailTarget>();
            }
        }

    }


    [DelimitedRecord("|")]
    [IgnoreEmptyLines()]
    internal class GenericPostContent
    {
        public string Id { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
    }

    [DelimitedRecord("|")]
    [IgnoreEmptyLines()]
    internal class FirstName
    {
        public string value { get; set; }
    }

    [DelimitedRecord("|")]
    [IgnoreEmptyLines()]
    internal class LastName
    {
        public string value { get; set; }
    }

    [DelimitedRecord("|")]
    [IgnoreEmptyLines()]
    internal class EmailTarget
    {
        public string value { get; set; }
    }



}
