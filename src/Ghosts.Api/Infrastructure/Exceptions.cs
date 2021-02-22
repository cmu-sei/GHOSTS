using System;

namespace Ghosts.Api.Infrastructure
{
    public class Exceptions
    {
        public class GhostsClientFormattingException : ArgumentException
        {
            public GhostsClientFormattingException() { }
            
            public GhostsClientFormattingException(string message) : base(message) { }
        }
    }
}