// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ghosts.Animator.Enums;
using Ghosts.Animator.Extensions;
using Ghosts.Animator.Models;
using Newtonsoft.Json;

namespace Ghosts.Animator
{
    public static class MilitaryRanks
    {
        public static MilitaryRank GetAll()
        {
            return GetAllEx();
        }

        public static MilitaryRank.Branch.Rank GetRank()
        {
            var ranks = new List<MilitaryRank.Branch.Rank>();
            var mil = GetAllEx();

            foreach (var branch in mil.Branches)
            {
                ranks.AddRange(branch.Ranks);
            }

            return GetRank(ranks);
        }

        public static MilitaryRank.Branch.Rank GetRankByBranch(MilitaryBranch branch)
        {
            var ranks = new List<MilitaryRank.Branch.Rank>();
            var mil = GetAllEx();

            foreach (var b in mil.Branches.Where(x => x.Name == branch.ToString()))
            {
                ranks.AddRange(b.Ranks);
            }

            return GetRank(ranks);
        }

        private static MilitaryRank GetAllEx()
        {
            var raw = File.ReadAllText("config/military_rank.json");
            var o = JsonConvert.DeserializeObject<MilitaryRank>(raw);
            return o;
        }

        private static MilitaryRank.Branch.Rank GetRank(List<MilitaryRank.Branch.Rank> ranks)
        {
            var u = ranks.Sum(o => o.Probability);
            var r = AnimatorRandom.Rand.NextDouble() * u;
            double sum = 0;
            var assignedRank = ranks.FirstOrDefault(rank => r <= (sum += rank.Probability));

            if (assignedRank != null)
            {
                assignedRank.Billet = GetBillet(assignedRank);
                var t = GetMOS(assignedRank);
                if (t is null)
                    return null;

                assignedRank.MOS = t[0];
                assignedRank.MOSID = t[1];

                return assignedRank;
            }

            return null;
        }

        private static string GetBillet(MilitaryRank.Branch.Rank rank)
        {
            var raw = File.ReadAllText("config/military_billet.json");
            var o = JsonConvert.DeserializeObject<BilletManager>(raw);

            var branch = o.Branches.FirstOrDefault(x => x.Name == rank.Branch.ToString());
            if (branch == null) return null;
            var possibleBillets = branch.Billets.Where(x => x.Pay == rank.Pay);
            var billets = possibleBillets as Billet[] ?? possibleBillets.ToArray();
            if (!billets.Any()) return null;
            var thisBillet = billets.RandomElement();
            return thisBillet?.Roles.RandomElement();
        }

        //Air Force CODEs are more complicated, but will require similar substitutions.
        private static string[] GetMOS(MilitaryRank.Branch.Rank rank)
        {
            var raw = File.ReadAllText("config/military_mos.json");
            var o = JsonConvert.DeserializeObject<MOSModels.MOSManager>(raw);

            var i = 0;
            string mosid = null;
            string mos = null;
            while (mos == null)
            {
                var m = o.Branches.FirstOrDefault(x => x.Name == rank.Branch.ToString());
                if (m != null)
                {
                    if (m.MOS == null || !m.MOS.Any()) return null;
                    var possibleMOS = m.MOS.RandomElement();
                    if (possibleMOS == null) continue;
                    var m1 = possibleMOS.Items.Where(x => PayToInt(x.Low) <= PayToInt(rank.Pay)
                        && PayToInt(x.High, "high") >= PayToInt(rank.Pay));
                    var e = m1 as MOSModels.Item[] ?? m1.ToArray();
                    if (e.Any())
                    {
                        var t = e.RandomElement();
                        mos = t.Name;
                        mosid = t.Code;

                        if (!string.IsNullOrEmpty(mos))
                            break;
                    }
                }

                i++;
                if (i > 50)
                    break;
            }

            if (rank.Branch == MilitaryBranch.USN && mosid != null)
            {
                if (rank.Pay[0] == 'W' && mosid[3] == 'X')
                {
                    mosid = mosid.Substring(0, 3) + '1';
                }
                if (rank.Pay[0] == 'O' && mosid[3] == 'X')
                {
                    mosid = mosid.Substring(0, 3) + '0';
                }
            }
            var ret = new string[] { mos, mosid };
            return ret;
        }

        //Converts ranks to integers so they can be comparable.
        //E-X >> 0 + X; W-X >> 100 + X; O-X >> 200 + X;
        private static int PayToInt(string pay, string bound = "low")
        {
            if (pay == null || pay == "")
            {
                if (bound == "low")
                {
                    return 0;
                }
                else //"high"
                {
                    return 300;
                }
            }

            int ret;
            if (pay[0] == 'E')
            {
                ret = 0;
            }
            else if (pay[0] == 'W')
            {
                ret = 100;
            }
            else //pay[0]=='O'
            {
                ret = 200;
            }

            string[] t;
            if (pay.Contains(","))
            {
                t = pay.Split(',');
            }
            else
            {
                t = pay.Split('-');
            }
            var x = int.Parse(t[1]);
            ret += x;
            return ret;
        }
    }

}
