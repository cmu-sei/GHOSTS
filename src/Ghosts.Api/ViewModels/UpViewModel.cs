// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using ghosts.api.Infrastructure.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ghosts.Api.ViewModels
{
    public class UpViewModel
    {
        public UpViewModel(IList<HistoryHealth> records)
        {
            Status = Machine.UpDownStatus.Unknown;
            Records = records;

            if (Records.Count < 1)
                //TODO: need to query if it is actually still up
                Status = Machine.UpDownStatus.Up;
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public Machine.UpDownStatus Status { get; }

        public IList<HistoryHealth> Records { get; }
    }
}
