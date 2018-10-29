// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using Ghosts.Api.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ghosts.Api.ViewModels
{
    public class UpViewModel
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public Machine.UpDownStatus Status { get; private set; }
        public IList<HistoryHealth> Records { get; private set; }

        public UpViewModel(IList<HistoryHealth> records)
        {
            this.Status = Machine.UpDownStatus.Unknown;
            this.Records = records;

            if (this.Records.Count < 1)
            {
                //TODO: need to query if it is actually still up
                this.Status = Machine.UpDownStatus.Up;
            }
        }
    }
}
