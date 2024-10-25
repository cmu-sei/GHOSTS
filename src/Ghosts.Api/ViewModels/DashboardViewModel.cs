// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;

namespace Ghosts.Api.ViewModels
{
    public class DashboardViewModel
    {
        public DashboardViewModel()
        {
            ChartLabels = new List<string>();
            ChartItems = new List<ChartItem>();
        }

        public double MachinesTracked { get; set; }
        public double ClientOperations { get; set; }
        public double HoursManaged { get; set; }
        public double MachinesWithHealthIssues { get; set; }
        public IList<string> ChartLabels { get; set; }
        public IList<ChartItem> ChartItems { get; set; }

        public class ChartItem
        {
            public ChartItem()
            {
                Data = new List<int>();
            }

            public string Label { get; set; }
            public IList<int> Data { get; set; }
        }
    }
}
