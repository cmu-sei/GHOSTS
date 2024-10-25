// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Domain.Code;

namespace ghosts.client.linux.Infrastructure
{

    public static class ClientConfigurationResolver
    {
        public static string Dictionary => ApplicationDetails.ConfigurationFiles.Dictionary(Program.Configuration.Content.Dictionary);
        public static string EmailContent => ApplicationDetails.ConfigurationFiles.EmailContent(Program.Configuration.Content.EmailContent);
        public static string EmailReply => ApplicationDetails.ConfigurationFiles.EmailReply(Program.Configuration.Content.EmailReply);
        public static string EmailDomain => ApplicationDetails.ConfigurationFiles.EmailDomain(Program.Configuration.Content.EmailDomain);
        public static string EmailOutside => ApplicationDetails.ConfigurationFiles.EmailOutside(Program.Configuration.Content.EmailOutside);
        public static string FileNames => ApplicationDetails.ConfigurationFiles.FileNames(Program.Configuration.Content.FileNames);
        public static string ChatMessages => ApplicationDetails.ConfigurationFiles.ChatMessages(Program.Configuration.Content.ChatMessages);

        public static string FirstNames => ApplicationDetails.ConfigurationFiles.FirstNames(Program.Configuration.Content.FirstNames);

        public static string LastNames => ApplicationDetails.ConfigurationFiles.LastNames(Program.Configuration.Content.LastNames);
        public static string EmailTargets => ApplicationDetails.ConfigurationFiles.EmailTargets(Program.Configuration.Content.EmailTargets);

        public static string GenericPostContent => ApplicationDetails.ConfigurationFiles.BlogContent(Program.Configuration.Content.GenericPostContent);

        public static string BlogContent => ApplicationDetails.ConfigurationFiles.BlogContent(Program.Configuration.Content.BlogContent);

        public static string BlogReply => ApplicationDetails.ConfigurationFiles.BlogReply(Program.Configuration.Content.BlogReply);
    }
}
