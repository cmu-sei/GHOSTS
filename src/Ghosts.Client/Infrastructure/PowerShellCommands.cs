// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using NLog;

namespace Ghosts.Client.Infrastructure;

/// <summary>
/// Stub for class that executes and collects PowerShell commands
/// //HACK:
/// </summary>
public class PowerShellCommands
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    public List<string> GetDomainEmailAddresses()
    {
        var list = new List<string>();
        using (var ps1 = PowerShell.Create())
        {
            var cmd = Program.Configuration.Email.EmailDomainSearchString;

            if (!string.IsNullOrEmpty(cmd))
            {

                ps1.AddScript(cmd);

                _log.Trace(cmd);

                var outputCollection = new PSDataCollection<PSObject>();
                outputCollection.DataAdded += OutputCollection_DataAdded;

                ps1.Streams.Error.DataAdded += Error_DataAdded;
                var result = ps1.BeginInvoke<PSObject, PSObject>(null, outputCollection);

                // do something else until execution has completed - could be other work
                while (result.IsCompleted == false)
                {
                    Thread.Sleep(1000);
                    // might want to place a timeout here...
                    _log.Trace("Waiting for cmd to complete");
                }

                _log.Trace("Execution has stopped. The pipeline state: " + ps1.InvocationStateInfo.State);

                list.AddRange(outputCollection.Select(outputItem => outputItem.BaseObject.ToString()));
            }
        }
        return list;
    }

    /// <summary>
    /// Event handler for when data is added to the output stream.
    /// </summary>
    /// <param name="sender">Contains the complete PSDataCollection of all output items.</param>
    /// <param name="e">Contains the index ID of the added collection item and the ID of the PowerShell instance this event belongs to.</param>
    void OutputCollection_DataAdded(object sender, DataAddedEventArgs e)
    {
        // do something when an object is written to the output stream
        _log.Trace(sender);
    }

    /// <summary>
    /// Event handler for when Data is added to the Error stream.
    /// </summary>
    /// <param name="sender">Contains the complete PSDataCollection of all error output items.</param>
    /// <param name="e">Contains the index ID of the added collection item and the ID of the PowerShell instance this event belongs to.</param>
    void Error_DataAdded(object sender, DataAddedEventArgs e)
    {
        // do something when an error is written to the error stream
        _log.Error(sender);
    }
}