// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Newtonsoft.Json;

namespace Ghosts.Client.Infrastructure.Browser;

public class ExtendedConfiguration
{
    public int Stickiness { get; set; }
    public int DepthMin { get; set; }
    public int DepthMax { get; set; }
    public object[] Sites { get; set; }

    public override string ToString()
    {
        return JsonConvert.SerializeObject(this);
    }

    public static ExtendedConfiguration Load(object o)
    {
        var commandArg = o.ToString();
        var result = new ExtendedConfiguration();
        if (commandArg.StartsWith("{"))
        {
            result = JsonConvert.DeserializeObject<ExtendedConfiguration>(commandArg);
            return result;
        }

        return result;
    }
}