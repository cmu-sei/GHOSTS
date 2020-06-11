using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FileHelpers;

namespace ghosts.tools.emailgenerator
{
    class Program
    {
        static void Main(string[] args)
        {

            var engine = new FileHelperEngine<EmailContent>();

            // To Read Use:
            var list = engine.ReadFile("config/email-content.csv");
            
            
            foreach (var item in list)
            {
                Console.WriteLine(item.Subject);
                Console.WriteLine("");
                Console.WriteLine(item.Body);
                Console.WriteLine("");
                Console.WriteLine("");

                Console.ReadLine();
                
            }


            Console.WriteLine("Done");
            Console.ReadLine();
            Console.ReadLine();


        }
    }

    [DelimitedRecord("|")]
    class EmailContent
    {
        public string Id { get; set; }
        
        public string Subject { get; set; }
        public string Body { get; set; }

        public string GetSubject()
        {


            var x = this.Subject;
            x = ReplaceCaseInsensitive(x, "<conflict_1_capital/>", "Camp Varick");
            x = ReplaceCaseInsensitive(x, "<conflict_1_name/>", "Simyra");
            x = ReplaceCaseInsensitive(x, "<conflict_1_peoples/>", "Symarians");
            x = ReplaceCaseInsensitive(x, "<conflict_1_president/>", "Ghengis Khan");
            x = ReplaceCaseInsensitive(x, "<localized_flashpoint_locale/>", "Pamir");
            x = ReplaceCaseInsensitive(x, "<friendly_nation_leader_lastname/>", "");
            x = ReplaceCaseInsensitive(x, "<friendly_nation_leader_name/>", "");
            x = ReplaceCaseInsensitive(x, "<friendly_nation_name/>", "");
            x = ReplaceCaseInsensitive(x, "<friendly_nation_peoples/>", "");
            x = ReplaceCaseInsensitive(x, "<commander_title/>", "");
            x = ReplaceCaseInsensitive(x, "<commander_name/>", "");
            x = ReplaceCaseInsensitive(x, "<commander_initials/>", "");
            x = ReplaceCaseInsensitive(x, "<commander_lastname/>", "");
            x = ReplaceCaseInsensitive(x, "<commander_email/>", "");
            x = ReplaceCaseInsensitive(x, "<commander_sub1/>", "");
            x = ReplaceCaseInsensitive(x, "< commander_sub2/>", "");
            x = ReplaceCaseInsensitive(x, "<us_president/>", "");
            x = ReplaceCaseInsensitive(x, "<iraq/>", "");
            x = ReplaceCaseInsensitive(x, "<iraqi/>", "");
            x = ReplaceCaseInsensitive(x, "<iran/>", "");
            x = ReplaceCaseInsensitive(x, "<iranian/>", "");
            x = ReplaceCaseInsensitive(x, "<china/>", "");
            x = ReplaceCaseInsensitive(x, "<chinese/>", "");
            x = ReplaceCaseInsensitive(x, "<russia/>", "");
            x = ReplaceCaseInsensitive(x, "<AZERBAIJAN/>", "");
            x = ReplaceCaseInsensitive(x, "<turkish/>", "");
            x = ReplaceCaseInsensitive(x, "<TURKEY/>", "");
            x = ReplaceCaseInsensitive(x, "<Pakistani/>", "");
            x = ReplaceCaseInsensitive(x, "<Pakistan/>", "");
            x = ReplaceCaseInsensitive(x, "<Palestinian/>", "");
            x = ReplaceCaseInsensitive(x, "<Palestine/>", "");
            x = ReplaceCaseInsensitive(x, "<Gaza/>", "");
            x = ReplaceCaseInsensitive(x, "<Korea/>", "");

            return x;
        }

        public string GetBody()
        {
            var x = this.Body;
            x = ReplaceCaseInsensitive(x, "<conflict_1_capital/>", "Simyra");
            x = ReplaceCaseInsensitive(x, "<conflict_1_name/>", "");
            x = ReplaceCaseInsensitive(x, "<conflict_1_peoples/>", "");
            x = ReplaceCaseInsensitive(x, "<conflict_1_president/>", "");
            x = ReplaceCaseInsensitive(x, "<localized_flashpoint_locale/>", "");
            x = ReplaceCaseInsensitive(x, "<friendly_nation_leader_lastname/>", "");
            x = ReplaceCaseInsensitive(x, "<friendly_nation_leader_name/>", "");
            x = ReplaceCaseInsensitive(x, "<friendly_nation_name/>", "");
            x = ReplaceCaseInsensitive(x, "<friendly_nation_peoples/>", "");
            x = ReplaceCaseInsensitive(x, "<commander_title/>", "");
            x = ReplaceCaseInsensitive(x, "<commander_name/>", "");
            x = ReplaceCaseInsensitive(x, "<commander_initials/>", "");
            x = ReplaceCaseInsensitive(x, "<commander_lastname/>", "");
            x = ReplaceCaseInsensitive(x, "<commander_email/>", "");
            x = ReplaceCaseInsensitive(x, "<commander_sub1/>", "");
            x = ReplaceCaseInsensitive(x, "< commander_sub2/>", "");
            x = ReplaceCaseInsensitive(x, "<us_president/>", "");
            x = ReplaceCaseInsensitive(x, "<iraq/>", "");
            x = ReplaceCaseInsensitive(x, "<iraqi/>", "");
            x = ReplaceCaseInsensitive(x, "<iran/>", "");
            x = ReplaceCaseInsensitive(x, "<iranian/>", "");
            x = ReplaceCaseInsensitive(x, "<china/>", "");
            x = ReplaceCaseInsensitive(x, "<chinese/>", "");
            x = ReplaceCaseInsensitive(x, "<russia/>", "");
            x = ReplaceCaseInsensitive(x, "<AZERBAIJAN/>", "");
            x = ReplaceCaseInsensitive(x, "<turkish/>", "");
            x = ReplaceCaseInsensitive(x, "<TURKEY/>", "");
            x = ReplaceCaseInsensitive(x, "<Pakistani/>", "");
            x = ReplaceCaseInsensitive(x, "<Pakistan/>", "");
            x = ReplaceCaseInsensitive(x, "<Palestinian/>", "");
            x = ReplaceCaseInsensitive(x, "<Palestine/>", "");
            x = ReplaceCaseInsensitive(x, "<Gaza/>", "");
            x = ReplaceCaseInsensitive(x, "<Korea/>", "");

            return x;
        }

        private static string ReplaceCaseInsensitive(string input, string search, string replacement)
        {
            string result = Regex.Replace(
                input,
                Regex.Escape(search),
                replacement.Replace("$", "$$"),
                RegexOptions.IgnoreCase
            );
            return result;
        }
    }
}
