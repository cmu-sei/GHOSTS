using FileHelpers;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ghosts.Client.Infrastructure.Browser
{
    /// <summary>
    /// The classes in this file assist with auto-generation of POST content
    /// </summary>
    public class PostContentManager
    {

        private static readonly Random _random = new Random();

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

        private string getRandomInitial(bool uppercase)
        {
            char c = (char)_random.Next((int)'a', ((int)'z') + 1);
            if (uppercase) return c.ToString().ToUpper();
            else return c.ToString();
        }

        //Capitalize first letter
        private string getCapitializeFirst(bool uppercase, string s)
        {
            if (uppercase)
            {
                string retval;
                if (s.Contains("-"))
                {
                    //handle conjoined last name, capitalize first letter of each name
                    var words = s.Split('-');
                    retval = words[0][0].ToString().ToUpper() + words[0].Substring(1) + "-" + words[1][0].ToString().ToUpper() + words[1].Substring(1);
                } else
                {
                    retval = s[0].ToString().ToUpper() + s.Substring(1);
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
            var seperator = seperators[_random.Next(0, seperators.Count())].ToString();
            if (_random.Next(0,10) == 0) lastName = lastName + "-" + lastName2;  //10% of names are conjoined
            var useInitials = _random.Next(0, 2)>0;
            var useFirstNameFirst = _random.Next(0, 2)>0;
            var useMiddleInitial = _random.Next(0, 2)>0;
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
            } else if (useFirstNameFirst)
            {
                if (useMiddleInitial) name = $"{getCapitializeFirst(useUpperCase, firstName)}{seperator}{getCapitializeFirst(useUpperCase, middleInitial)}{seperator}{getCapitializeFirst(useUpperCase, lastName)}";
                else name = $"{getCapitializeFirst(useUpperCase, firstName)}{seperator}{getCapitializeFirst(useUpperCase, lastName)}";
            } else
            {
                if (useMiddleInitial) name = $"{getCapitializeFirst(useUpperCase, lastName)}{seperator}{getCapitializeFirst(useUpperCase, firstName)}{seperator}{getCapitializeFirst(useUpperCase, middleInitial)}";
                else name = $"{getCapitializeFirst(useUpperCase, lastName)}{seperator}{getCapitializeFirst(useUpperCase, firstName)}";
            }

            Email = $"{name}@{emailTarget}";

        }

        

        public void GenericContentNext()
        {

            var total = this.GenericContent.Count;

            if (total <= 0)
            {
                this.Subject = "nogenericcontentavailable";
                this.Body = "nogenericcontentavailable";
                return;
            };


            var o = this.GenericContent[_random.Next(0, total)];


            this.Subject = o.Subject.Replace("\\n", "\n");
            this.Body = o.Body.Replace("\\n", "\n");
        }

        
        public void LoadAllContent()
        {
            try
            {
                var engine = new FileHelperEngine<GenericPostContent>();
                engine.Encoding = Encoding.UTF8;
                this.GenericContent = engine.ReadFile(ClientConfigurationResolver.GenericPostContent).ToList();
            }
            catch (Exception e)
            {
                _log.Error($"Post Generic content file {ClientConfigurationResolver.GenericPostContent} could not be loaded: {e}");
                this.GenericContent = new List<GenericPostContent>();
            }
            try
            {
                var engine = new FileHelperEngine<FirstName>();
                engine.Encoding = Encoding.UTF8;
                this.firstNames = engine.ReadFile(ClientConfigurationResolver.FirstNames).ToList();
            }
            catch (Exception e)
            {
                _log.Error($"First Name content file {ClientConfigurationResolver.FirstNames} could not be loaded: {e}");
                this.firstNames = new List<FirstName>();
            }
            try
            {
                var engine = new FileHelperEngine<LastName>();
                engine.Encoding = Encoding.UTF8;
                this.lastNames = engine.ReadFile(ClientConfigurationResolver.LastNames).ToList();
            }
            catch (Exception e)
            {
                _log.Error($"Last Name content file {ClientConfigurationResolver.LastNames} could not be loaded: {e}");
                this.lastNames = new List<LastName>();
            }
            try
            {
                var engine = new FileHelperEngine<EmailTarget>();
                engine.Encoding = Encoding.UTF8;
                this.emailTargets = engine.ReadFile(ClientConfigurationResolver.EmailTargets).ToList();
            }
            catch (Exception e)
            {
                _log.Error($"Last Name content file {ClientConfigurationResolver.EmailTargets} could not be loaded: {e}");
                this.emailTargets = new List<EmailTarget>();
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
