// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using Ghosts.Domain;

namespace Ghosts.Api.ViewModels
{
    /// <summary>
    /// Raw terminal commands to run once on a machine, in order
    /// </summary>
    public class MachineCommandsViewModel
    {
        /// <summary>
        /// Bash, Command (cmd.exe), or PowerShell
        /// </summary>
        public HandlerType Shell { get; set; } = HandlerType.Bash;

        public List<string> Commands { get; set; }
    }
}
