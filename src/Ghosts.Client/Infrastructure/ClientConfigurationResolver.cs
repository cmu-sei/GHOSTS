using Ghosts.Domain.Code;

namespace Ghosts.Client.Infrastructure
{
    public static class ClientConfigurationResolver
    {
        public static string Dictionary => ApplicationDetails.ConfigurationFiles.Dictionary(Program.Configuration.Content.Dictionary);
        public static string EmailContent => ApplicationDetails.ConfigurationFiles.EmailContent(Program.Configuration.Content.EmailContent);
        public static string EmailReply => ApplicationDetails.ConfigurationFiles.EmailReply(Program.Configuration.Content.EmailReply);
        public static string EmailDomain => ApplicationDetails.ConfigurationFiles.EmailDomain(Program.Configuration.Content.EmailDomain);
        public static string EmailOutside => ApplicationDetails.ConfigurationFiles.EmailOutside(Program.Configuration.Content.EmailOutside);
        public static string FileNames => ApplicationDetails.ConfigurationFiles.FileNames(Program.Configuration.Content.FileNames);
    }
}
