// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

namespace Ghosts.Domain.Code
{
    public class ReportItem
    {
        public string Handler { get; set; }
        public string Command { get; set; }
        public string Arg { get; set; }
        public string Trackable { get; set; }
        public string Result { get; set; }
    }
}
