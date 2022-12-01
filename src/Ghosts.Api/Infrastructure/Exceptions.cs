// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

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