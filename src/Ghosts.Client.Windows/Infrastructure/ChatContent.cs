using FileHelpers;
using Ghosts.Client.Infrastructure.Browser;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ghosts.Client.Infrastructure
{
    public class ChatContent
    {
        internal IList<ChatMessage> Messages { private set; get; }

        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private static readonly Random _random = new Random();

        public ChatContent()
        {
            LoadAllContent();
        }

        public string MessageNext()
        {
            var total = this.Messages.Count;

            if (total <= 0) return "nochatcontentavailable";

            ChatMessage o = this.Messages[_random.Next(0, total)];
            return o.value.Replace("\\n", "\n");
        }

        public void LoadAllContent()
        {
            try
            {
                var engine = new FileHelperEngine<ChatMessage>();
                engine.Encoding = Encoding.UTF8;
                this.Messages = engine.ReadFile(ClientConfigurationResolver.ChatMessages).ToList();
            }
            catch (Exception e)
            {
                _log.Error($"Chat content file {ClientConfigurationResolver.ChatMessages} could not be loaded: {e}");
                this.Messages = new List<ChatMessage>();
            }
        }

        [DelimitedRecord("|")]
        [IgnoreEmptyLines()]
        internal class ChatMessage
        {
            public string value { get; set; }
           
        }
    }
}
