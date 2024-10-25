// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ghosts.Animator.Enums;
using Ghosts.Animator.Extensions;
using Ghosts.Animator.Models;
using Newtonsoft.Json;

namespace Ghosts.Animator
{
    public static class Education
    {
        /*
         * Possible Improvements: 
         * DegreeType (BS vs BA) is determined by field of study. Generating this in a representatively random way would be an improvement.
         * Major within field is chosen as an evenly distributed random, this could also be improved by making it representatively random.
         * Alternate community college naming format (__ County Community College vs __ Community College)
         *      Is this an improvement? I don't want to have both Monroe Community College and Monroe County Community College show up
        */
        public static EducationProfile GetEducationProfile()
        {
            var o = new EducationProfile
            {
                Degrees = GetEducation()
            };
            return o;
        }
        public static EducationProfile GetMilEducationProfile(MilitaryRank.Branch.Rank rank)
        {
            var o = new EducationProfile
            {
                Degrees = GetMilEducation(rank.Pay)
            };

            return DegreeMOSRequirements(o, rank);
        }

        private static EducationProfile DegreeMOSRequirements(EducationProfile o, MilitaryRank.Branch.Rank rank)
        {
            var input = File.ReadAllText("config/majors.json");
            var t = JsonConvert.DeserializeObject<MajorManager>(input, new JsonSerializerSettings { FloatParseHandling = FloatParseHandling.Double });

            //check for MOS degree requirements
            if (rank.Branch == MilitaryBranch.USARMY && rank.Pay[0] == 'O')
            {
                //check MOS
                //Engineering: 01C, 02C, 12A, 26A, 26B, 26Z,(26*), 51R, 51S
                //Medical: 05A, 60*, 63*(Dental), 64*(length==3) (Veterinary), 65*, 66*, 67*(length==3), 70*, 71*, 72*, 73*
                //Law: 27A, 27B
                //Chaplains: 56A, 56D, 56X, (56*) TODO HERE
                //Professors: 47* (80% PhD, other 20% masters)
                //Engineering: 47D, 47P, 47R, 47F
                //Law: 47E
                foreach (var i in o.Degrees)
                {
                    //Engineering
                    if (rank.MOSID == "01C" || rank.MOSID == "02C" || rank.MOSID == "12A" ||
                        rank.MOSID.Substring(0, 2) == "26" || rank.MOSID == "51R"
                        || rank.MOSID == "51S")
                    {
                        var t1 = t.MajorDegreeLevels.FirstOrDefault(x => x.Level == "Bachelors");
                        var t2 = t1.Fields.FirstOrDefault(x => x.Name == "Engineering");
                        if (i.Level == DegreeLevel.Bachelors)
                        {
                            i.DegreeType = t2.DegreeType;
                            i.Major = t2.Majors.RandomElement();
                            break;
                        }
                        if (i == o.Degrees.Last()) //correct degreelevel didn't exist and must be added
                        {
                            o.Degrees.Add(new EducationProfile.Degree(DegreeLevel.Bachelors,
                                major: t2.Majors.RandomElement() + ",B.S.", GetSchool()));
                            break;
                        }
                    }
                    //Medical
                    if (rank.MOSID == "05A" || rank.MOSID.Substring(0, 2) == "60" ||
                        rank.MOSID.Substring(0, 2) == "63" || rank.MOSID.Substring(0, 2) == "64" ||
                        rank.MOSID.Substring(0, 2) == "65" || rank.MOSID.Substring(0, 2) == "66" ||
                        rank.MOSID.Substring(0, 2) == "67" || rank.MOSID.Substring(0, 2) == "70" ||
                        rank.MOSID.Substring(0, 2) == "71" || rank.MOSID.Substring(0, 2) == "72" ||
                        rank.MOSID.Substring(0, 2) == "73")
                    {
                        var t1 = t.MajorDegreeLevels.FirstOrDefault(x => x.Level == "Professional");
                        var t2 = t1.Fields.FirstOrDefault(x => x.Name == "Health Professions And Related Programs");
                        if (i.Level == DegreeLevel.Professional)
                        {
                            i.DegreeType = t2.DegreeType;
                            i.Major = t2.Majors.RandomElement();
                            break;
                        }
                        if (i == o.Degrees.Last()) //correct degreelevel didn't exist and must be added
                        {
                            o.Degrees.Add(new EducationProfile.Degree(DegreeLevel.Professional,
                                t2.Majors.RandomElement() + ",M.D.", GetSchool()));
                            break;
                        }
                    }
                    //Law
                    if (rank.MOSID == "27A" || rank.MOSID == "27B")
                    {
                        var t1 = t.MajorDegreeLevels.FirstOrDefault(x => x.Level == "Professional");
                        var t2 = t1.Fields.FirstOrDefault(x => x.Name == "Legal Professions And Studies");
                        if (i.Level == DegreeLevel.Professional)
                        {
                            i.DegreeType = t2.DegreeType;
                            i.Major = t2.Majors.RandomElement();
                            break;
                        }
                        if (i == o.Degrees.Last()) //correct degreelevel didn't exist and must be added
                        {
                            o.Degrees.Add(new EducationProfile.Degree(DegreeLevel.Professional,
                                t2.Majors.RandomElement() + ",J.D.", GetSchool()));
                            break;
                        }
                    }
                    //Chaplain
                    if (rank.MOSID.Substring(0, 2) == "56")
                    {
                        var t1 = t.MajorDegreeLevels.FirstOrDefault(x => x.Level == "Professional");
                        var t2 = t1.Fields.FirstOrDefault(x => x.Name == "Theology And Religious Vocations");
                        if (i.Level == DegreeLevel.Professional)
                        {
                            i.DegreeType = t2.DegreeType;
                            i.Major = t2.Majors.RandomElement();
                            break;
                        }
                        if (i == o.Degrees.Last()) //correct degreelevel didn't exist and must be added
                        {
                            o.Degrees.Add(new EducationProfile.Degree(DegreeLevel.Professional,
                                t2.Majors.RandomElement() + ",Th.D.", GetSchool()));
                            break;
                        }
                    }
                    //Professors: 47* (80% PhD, other 20% masters)
                    //Engineering: 47D, 47P, 47R, 47F
                    //Law: 47E
                    if (rank.MOSID.Substring(0, 2) == "47")
                    {
                        var t1 = t.MajorDegreeLevels.FirstOrDefault(x => x.Level == "Doctorate");
                        Field t2;
                        if (rank.MOSID == "47E") //Law
                        {
                            t2 = t1.Fields.FirstOrDefault(x => x.Name == "Legal Professions And Studies");
                        }
                        else if (rank.MOSID == "47D" || rank.MOSID == "47P" || rank.MOSID == "47R" ||
                            rank.MOSID == "47F")//Engineering
                        {
                            t2 = t1.Fields.FirstOrDefault(x => x.Name == "Engineering");
                        }
                        else //TODO: Replace the random selection with selection by MOS
                        {
                            t2 = t1.Fields.RandomElement();
                        }
                        if (i.Level == DegreeLevel.Professional)
                        {
                            i.DegreeType = t2.DegreeType;
                            i.Major = t2.Majors.RandomElement();
                            break;
                        }
                        if (i == o.Degrees.Last()) //correct degreelevel didn't exist and must be added
                        {
                            o.Degrees.Add(new EducationProfile.Degree(DegreeLevel.Professional,
                                t2.Majors.RandomElement() + ",Ph.D.", GetSchool()));
                            break;
                        }
                    }
                }
            }
            else if (rank.Branch == MilitaryBranch.USMC && rank.Pay[0] == 'O')
            {
                //check MOS
                //Engineering: 0610, 0630, 0650, 0670, 1301, 1302, 1310, 6004, 8820, 8824, 8826, 8831, 8832
                //Law: 44**
                //Nuclear Science: 5702
                foreach (var i in o.Degrees)
                {
                    //Engineering
                    if (rank.MOSID == "0610" || rank.MOSID == "0630" || rank.MOSID == "0650" ||
                        rank.MOSID == "0670" || rank.MOSID == "1301" || rank.MOSID == "1302" ||
                        rank.MOSID == "6004" || rank.MOSID == "8820" || rank.MOSID == "8824" ||
                        rank.MOSID == "8826" || rank.MOSID == "8831" || rank.MOSID == "8832")
                    {
                        var t1 = t.MajorDegreeLevels.FirstOrDefault(x => x.Level == "Bachelors");
                        var t2 = t1.Fields.FirstOrDefault(x => x.Name == "Engineering");
                        if (i.Level == DegreeLevel.Bachelors)
                        {
                            i.DegreeType = t2.DegreeType;
                            i.Major = t2.Majors.RandomElement();
                            break;
                        }
                        if (i == o.Degrees.Last()) //correct degreelevel didn't exist and must be added
                        {
                            o.Degrees.Add(new EducationProfile.Degree(DegreeLevel.Bachelors,
                                t2.Majors.RandomElement() + ",B.S.", GetSchool()));
                            break;
                        }
                    }
                    //Law
                    if (rank.MOSID.Substring(0, 2) == "44")
                    {
                        var t1 = t.MajorDegreeLevels.FirstOrDefault(x => x.Level == "Professional");
                        var t2 = t1.Fields.FirstOrDefault(x => x.Name == "Legal Professions And Studies");
                        if (i.Level == DegreeLevel.Professional)
                        {
                            i.DegreeType = t2.DegreeType;
                            i.Major = t2.Majors.RandomElement();
                            break;
                        }
                        if (i == o.Degrees.Last()) //correct degreelevel didn't exist and must be added
                        {
                            o.Degrees.Add(new EducationProfile.Degree(DegreeLevel.Professional,
                                t2.Majors.RandomElement() + ",J.D.", GetSchool()));
                            break;
                        }
                    }
                    if (rank.MOSID == "5702")
                    {
                        var num = AnimatorRandom.Rand.Next(2);
                        if (i.Level == DegreeLevel.Bachelors)
                        {
                            i.DegreeType = "B.S.";
                            if (num < 1)
                            {
                                i.Major = "Nuclear Physics";
                            }
                            else
                            {
                                i.Major = "Nuclear Engineering";
                            }
                            break;
                        }
                        if (i.Level == DegreeLevel.Masters)
                        {
                            i.DegreeType = "M.S.";
                            if (num < 1)
                            {
                                i.Major = "Nuclear Physics";
                            }
                            else
                            {
                                i.Major = "Nuclear Engineering";
                            }
                            break;
                        }
                        if (i.Level == DegreeLevel.Doctorate)
                        {
                            i.DegreeType = "Ph.D.";
                            if (num < 1)
                            {
                                i.Major = "Nuclear Physics";
                            }
                            else
                            {
                                i.Major = "Nuclear Engineering";
                            }
                            break;
                        }
                        if (i == o.Degrees.Last()) //correct degreelevel didn't exist and must be added
                        {
                            if (num < 1)
                            {
                                o.Degrees.Add(new EducationProfile.Degree(DegreeLevel.Bachelors,
                                    "Nuclear Engineering,B.S.", GetSchool()));
                            }
                            else
                            {
                                o.Degrees.Add(new EducationProfile.Degree(DegreeLevel.Bachelors,
                                    "Nuclear Physics,B.S.", GetSchool()));
                            }
                            break;
                        }
                    }
                }
            }
            else if (rank.Branch == MilitaryBranch.USN && rank.Pay[0] == 'O')
            {
                //check MOS
                //Engineering: 122X, 144X, 150X, 151X, 152X, 184X, 146X, 613X, 623X, 653X
                //Medical: 210X, 220X, 230X, 270X, 290X, 510X
                //Law: 250X, 655X
                //Chaplain: 410X
                //Education: 1230
                foreach (var i in o.Degrees)
                {
                    //Engineering
                    if (rank.MOSID == "122X" || rank.MOSID == "144X" || rank.MOSID == "150X" ||
                        rank.MOSID == "151X" || rank.MOSID == "152X" || rank.MOSID == "184X" ||
                        rank.MOSID == "146X" || rank.MOSID == "613X" || rank.MOSID == "623X" ||
                        rank.MOSID == "653X")
                    {
                        var t1 = t.MajorDegreeLevels.FirstOrDefault(x => x.Level == "Bachelors");
                        var t2 = t1.Fields.FirstOrDefault(x => x.Name == "Engineering");
                        if (i.Level == DegreeLevel.Bachelors)
                        {
                            i.DegreeType = t2.DegreeType;
                            i.Major = t2.Majors.RandomElement();
                            break;
                        }
                        if (i == o.Degrees.Last()) //correct degreelevel didn't exist and must be added
                        {
                            o.Degrees.Add(new EducationProfile.Degree(DegreeLevel.Bachelors,
                                t2.Majors.RandomElement() + ",B.S.", GetSchool()));
                            break;
                        }
                    }
                    //Medical
                    if (rank.MOSID == "210X" || rank.MOSID == "220X" || rank.MOSID == "230X" ||
                        rank.MOSID == "270X" || rank.MOSID == "290X" || rank.MOSID == "510X")
                    {
                        var t1 = t.MajorDegreeLevels.FirstOrDefault(x => x.Level == "Professional");
                        var t2 = t1.Fields.FirstOrDefault(x => x.Name == "Health Professions And Related Programs");
                        if (i.Level == DegreeLevel.Professional)
                        {
                            i.DegreeType = t2.DegreeType;
                            i.Major = t2.Majors.RandomElement();
                            break;
                        }
                        if (i == o.Degrees.Last()) //correct degreelevel didn't exist and must be added
                        {
                            o.Degrees.Add(new EducationProfile.Degree(DegreeLevel.Professional,
                                t2.Majors.RandomElement() + ",M.D.", GetSchool()));
                            break;
                        }
                    }
                    //Law
                    if (rank.MOSID == "250X" || rank.MOSID == "655X")
                    {
                        var t1 = t.MajorDegreeLevels.FirstOrDefault(x => x.Level == "Professional");
                        var t2 = t1.Fields.FirstOrDefault(x => x.Name == "Legal Professions And Studies");
                        if (i.Level == DegreeLevel.Professional)
                        {
                            i.DegreeType = t2.DegreeType;
                            i.Major = t2.Majors.RandomElement();
                            break;
                        }
                        if (i == o.Degrees.Last()) //correct degreelevel didn't exist and must be added
                        {
                            o.Degrees.Add(new EducationProfile.Degree(DegreeLevel.Professional,
                                t2.Majors.RandomElement() + ",J.D.", GetSchool()));
                            break;
                        }
                    }
                    //Chaplain
                    if (rank.MOSID == "410X")
                    {
                        var t1 = t.MajorDegreeLevels.FirstOrDefault(x => x.Level == "Professional");
                        var t2 = t1.Fields.FirstOrDefault(x => x.Name == "Theology And Religious Vocations");
                        if (i.Level == DegreeLevel.Professional)
                        {
                            i.DegreeType = t2.DegreeType;
                            i.Major = t2.Majors.RandomElement();
                            break;
                        }
                        if (i == o.Degrees.Last()) //correct degreelevel didn't exist and must be added
                        {
                            o.Degrees.Add(new EducationProfile.Degree(DegreeLevel.Professional,
                                t2.Majors.RandomElement() + ",Th.D.", GetSchool()));
                            break;
                        }
                    }
                    //Education
                    if (rank.MOSID == "1230")
                    {
                        var t1 = t.MajorDegreeLevels.FirstOrDefault(x => x.Level == "Doctorate");
                        var t2 = t1.Fields.RandomElement();
                        if (i.Level == DegreeLevel.Doctorate)
                        {
                            i.DegreeType = t2.DegreeType;
                            i.Major = t2.Majors.RandomElement();
                            break;
                        }
                        if (i == o.Degrees.Last()) //correct degreelevel didn't exist and must be added
                        {
                            o.Degrees.Add(new EducationProfile.Degree(DegreeLevel.Doctorate,
                                t2.Majors.RandomElement() + ",Ph.D.", GetSchool()));
                            break;
                        }
                    }
                }
            }
            else if (rank.Branch == MilitaryBranch.USAF && rank.Pay != null && rank.Pay[0] == 'O')
            {
                //check MOS
                //Engineer: 32EX, 61DX, 62EX, 62EX*
                //Medical: 4***
                //Law: 51JX
                //Chaplain: 52RX, 92RO
                foreach (var i in o.Degrees)
                {
                    if (rank.MOSID == "32EX" || rank.MOSID == "61DX" || rank.MOSID == "62EX"
                        || rank.MOSID.Contains("62EX")) //Engineering
                    {
                        var t1 = t.MajorDegreeLevels.FirstOrDefault(x => x.Level == "Bachelors");
                        var t2 = t1.Fields.FirstOrDefault(x => x.Name == "Engineering");
                        if (i.Level == DegreeLevel.Bachelors)
                        {
                            i.DegreeType = t2.DegreeType;
                            i.Major = t2.Majors.RandomElement();
                            break;
                        }
                        if (i == o.Degrees.Last()) //correct degreelevel didn't exist and must be added
                        {
                            o.Degrees.Add(new EducationProfile.Degree(DegreeLevel.Bachelors,
                                t2.Majors.RandomElement() + ",B.S.", GetSchool()));
                            break;
                        }
                    }
                    if (rank.MOSID.Substring(0, 1) == "4") //Medical
                    {
                        var t1 = t.MajorDegreeLevels.FirstOrDefault(x => x.Level == "Professional");
                        var t2 = t1.Fields.FirstOrDefault(x => x.Name == "Health Professions And Related Programs");
                        if (i.Level == DegreeLevel.Professional)
                        {
                            i.DegreeType = t2.DegreeType;
                            i.Major = t2.Majors.RandomElement();
                            break;
                        }
                        if (i == o.Degrees.Last()) //correct degreelevel didn't exist and must be added
                        {
                            o.Degrees.Add(new EducationProfile.Degree(DegreeLevel.Professional,
                                t2.Majors.RandomElement() + ",M.D.", GetSchool()));
                            break;
                        }
                    }
                    if (rank.MOSID == "51JX") //Law
                    {
                        var t1 = t.MajorDegreeLevels.FirstOrDefault(x => x.Level == "Professional");
                        var t2 = t1.Fields.FirstOrDefault(x => x.Name == "Legal Professions And Studies");
                        if (i.Level == DegreeLevel.Professional)
                        {
                            i.DegreeType = t2.DegreeType;
                            i.Major = t2.Majors.RandomElement();
                            break;
                        }
                        if (i == o.Degrees.Last()) //correct degreelevel didn't exist and must be added
                        {
                            o.Degrees.Add(new EducationProfile.Degree(DegreeLevel.Professional,
                                t2.Majors.RandomElement() + ",J.D.", GetSchool()));
                            break;
                        }
                    }
                    if (rank.MOSID == "52RX" || rank.MOSID == "92RO") //Chaplain
                    {
                        var t1 = t.MajorDegreeLevels.FirstOrDefault(x => x.Level == "Professional");
                        var t2 = t1.Fields.FirstOrDefault(x => x.Name == "Theology And Religious Vocations");
                        if (i.Level == DegreeLevel.Professional)
                        {
                            i.DegreeType = t2.DegreeType;
                            i.Major = t2.Majors.RandomElement();
                            break;
                        }
                        if (i == o.Degrees.Last()) //correct degreelevel didn't exist and must be added
                        {
                            o.Degrees.Add(new EducationProfile.Degree(DegreeLevel.Professional,
                                t2.Majors.RandomElement() + ",Th.D.", GetSchool()));
                            break;
                        }
                    }
                }
            }
            else if (rank.Branch == MilitaryBranch.USCG && rank.Pay[0] == 'O')
            {
                //check MOS
                //Engineer: ENG
                //Medical: Medical
                foreach (var i in o.Degrees)
                {
                    if (rank.MOSID == "ENG")
                    {
                        var t1 = t.MajorDegreeLevels.FirstOrDefault(x => x.Level == "Bachelors");
                        var t2 = t1.Fields.FirstOrDefault(x => x.Name == "Engineering");
                        if (i.Level == DegreeLevel.Bachelors)
                        {
                            i.DegreeType = t2.DegreeType;
                            i.Major = t2.Majors.RandomElement();
                            break;
                        }
                        if (i == o.Degrees.Last()) //correct degreelevel didn't exist and must be added
                        {
                            o.Degrees.Add(new EducationProfile.Degree(DegreeLevel.Bachelors,
                                t2.Majors.RandomElement() + ",B.S.", GetSchool()));
                            break;
                        }
                    }
                    if (rank.MOSID == "Medical")
                    {
                        var t1 = t.MajorDegreeLevels.FirstOrDefault(x => x.Level == "Professional");
                        var t2 = t1.Fields.FirstOrDefault(x => x.Name == "Health Professions And Related Programs");
                        if (i.Level == DegreeLevel.Professional)
                        {
                            i.DegreeType = t2.DegreeType;
                            i.Major = t2.Majors.RandomElement();
                            break;
                        }
                        if (i == o.Degrees.Last()) //correct degreelevel didn't exist and must be added
                        {
                            o.Degrees.Add(new EducationProfile.Degree(DegreeLevel.Professional,
                                t2.Majors.RandomElement() + ",M.D.", GetSchool()));
                            break;
                        }
                    }
                }
            }
            return o;
        }

        //Statistics used for logic representative of US population
        public static List<EducationProfile.Degree> GetEducation()
        {
            List<EducationProfile.Degree> l = new List<EducationProfile.Degree>();
            var rd = AnimatorRandom.Rand.NextDouble();
            if (rd < 0.1)
            {
                //no high school
                l.Add(new EducationProfile.Degree(DegreeLevel.None, "", null));
            }
            else if (rd < 0.45)
            {
                //bachelors
                var temps = GetSchool();
                l.Add(new EducationProfile.Degree(DegreeLevel.Bachelors,
                    GetMajor(DegreeLevel.Bachelors), temps));
                //check for double major
                rd = AnimatorRandom.Rand.NextDouble();
                if (rd < 0.125)
                {
                    //double major
                    l.Add(new EducationProfile.Degree(DegreeLevel.Bachelors,
                        GetMajor(DegreeLevel.Bachelors), temps));
                }
                //check for grad degree
                rd = AnimatorRandom.Rand.NextDouble();
                if (rd < 0.37) //has a grad degree
                {
                    rd = AnimatorRandom.Rand.NextDouble();
                    if (rd < 0.07)
                    {
                        if (rd < 0.05)
                        {
                            //Masters and PhD
                            temps = GetSchool();
                            l.Add(new EducationProfile.Degree(DegreeLevel.Masters,
                                GetMajor(DegreeLevel.Masters), temps));
                            l.Add(new EducationProfile.Degree(DegreeLevel.Doctorate,
                                GetMajor(DegreeLevel.Doctorate), temps));
                        }
                        else
                        {
                            //Just PhD
                            l.Add(new EducationProfile.Degree(DegreeLevel.Doctorate,
                                GetMajor(DegreeLevel.Doctorate), GetSchool(DegreeLevel.Doctorate)));
                        }
                    }
                    else if (rd < 0.22)
                    {
                        //Professional
                        var t = GetMajor(DegreeLevel.Professional);
                        l.Add(new EducationProfile.Degree(DegreeLevel.Professional,
                                t, GetSchool(DegreeLevel.Professional, t.Split(',')[1])));
                    }
                    else
                    {
                        //Masters
                        l.Add(new EducationProfile.Degree(DegreeLevel.Masters,
                                GetMajor(DegreeLevel.Masters), GetSchool(DegreeLevel.Masters)));
                    }
                }

            }
            else if (rd < 0.55)
            {
                rd = AnimatorRandom.Rand.NextDouble();
                //associates
                l.Add(new EducationProfile.Degree(DegreeLevel.Associates,
                    GetMajor(DegreeLevel.Associates), GetSchool(DegreeLevel.Associates)));
                if (rd < .15) //associates and bachelors
                {
                    l.Add(new EducationProfile.Degree(DegreeLevel.Bachelors,
                    GetMajor(DegreeLevel.Bachelors), GetSchool()));
                }
            }
            else
            {
                rd = AnimatorRandom.Rand.NextDouble();
                if (rd < 0.9)
                {
                    var wasAdded = false;
                    //high school
                    if (Npc.NpcProfile != null)
                    {
                        foreach (var address in Npc.NpcProfile.Address)
                        {
                            l.Add(new EducationProfile.Degree(DegreeLevel.HSDiploma, "", new School { Name = $"{address.City} High School", Location = $"{address.City}, USA" }));
                            wasAdded = true;
                            break;
                        }
                    }
                    if (!wasAdded)
                        l.Add(new EducationProfile.Degree(DegreeLevel.HSDiploma, "", new School()));
                }
                else
                {
                    var wasAdded = false;
                    //GED
                    if (Npc.NpcProfile != null)
                    {
                        foreach (var address in Npc.NpcProfile.Address)
                        {
                            l.Add(new EducationProfile.Degree(DegreeLevel.GED, "", new School { Name = $"{address.City} High School", Location = $"{address.City}, USA" }));
                            wasAdded = true;
                            break;
                        }
                    }
                    if (!wasAdded)
                        l.Add(new EducationProfile.Degree(DegreeLevel.GED, "", new School()));
                }
            }
            return l;
        }

        //Restricted to US Schools
        public static List<EducationProfile.Degree> GetMilEducation(string rank)
        {
            if (string.IsNullOrEmpty(rank))
                return new List<EducationProfile.Degree>();

            List<EducationProfile.Degree> l = new List<EducationProfile.Degree>();
            //read military rank probabilities
            var input = File.ReadAllText("config/military_education.json");
            var o = JsonConvert.DeserializeObject<RankDegreeProbabilityManager>(input, new JsonSerializerSettings { FloatParseHandling = FloatParseHandling.Double });
            RankDegreeProbability r;
            //pull probabilities by rank
            switch (rank)
            {
                case "E-1":
                    r = o.RankDegreeProbabilities[0];
                    break;
                case "E-2":
                    r = o.RankDegreeProbabilities[1];
                    break;
                case "E-3":
                    r = o.RankDegreeProbabilities[2];
                    break;
                case "E-4":
                    r = o.RankDegreeProbabilities[3];
                    break;
                case "E-5":
                    r = o.RankDegreeProbabilities[4];
                    break;
                case "E-6":
                    r = o.RankDegreeProbabilities[5];
                    break;
                case "E-7":
                    r = o.RankDegreeProbabilities[6];
                    break;
                case "E-8":
                    r = o.RankDegreeProbabilities[7];
                    break;
                case "E-9":
                    r = o.RankDegreeProbabilities[8];
                    break;
                case "W-1":
                    r = o.RankDegreeProbabilities[9];
                    break;
                case "W-2":
                    r = o.RankDegreeProbabilities[10];
                    break;
                case "W-3":
                    r = o.RankDegreeProbabilities[11];
                    break;
                case "W-4":
                    r = o.RankDegreeProbabilities[12];
                    break;
                case "O-1":
                    r = o.RankDegreeProbabilities[13];
                    break;
                case "O-2":
                    r = o.RankDegreeProbabilities[14];
                    break;
                case "O-3":
                    r = o.RankDegreeProbabilities[15];
                    break;
                case "O-4":
                    r = o.RankDegreeProbabilities[16];
                    break;
                case "O-5":
                    r = o.RankDegreeProbabilities[17];
                    break;
                case "O-6":
                    r = o.RankDegreeProbabilities[18];
                    break;
                case "O-7":
                    r = o.RankDegreeProbabilities[19];
                    break;
                default:
                    //Rank is higher than E-9, W-4, or O-7 respectively.
                    if (rank.Split('-')[0] == "E")
                    {
                        r = o.RankDegreeProbabilities[8];
                    }
                    else if (rank.Split('-')[0] == "W")
                    {
                        r = o.RankDegreeProbabilities[12];
                    }
                    else
                    {
                        r = o.RankDegreeProbabilities[19];
                    }
                    break;
            }
            var rd = AnimatorRandom.Rand.NextDouble();
            if (rd < r.AssociatesProbability)
            {
                l.Add(new EducationProfile.Degree(DegreeLevel.Associates,
                    GetMajor(DegreeLevel.Associates), GetUSSchool(DegreeLevel.Associates)));
            }
            rd = AnimatorRandom.Rand.NextDouble();
            if (rd < r.BachelorsProbability)
            {
                l.Add(new EducationProfile.Degree(DegreeLevel.Bachelors,
                    GetMajor(DegreeLevel.Bachelors), GetUSSchool()));
            }
            rd = AnimatorRandom.Rand.NextDouble();
            if (rd < r.MastersProbability)
            {
                //Check to make sure there's a bachelor's degree
                if (l.Count == 0 || (l.Count == 1 && l[0].Level != DegreeLevel.Bachelors))
                {
                    l.Add(new EducationProfile.Degree(DegreeLevel.Bachelors,
                        GetMajor(DegreeLevel.Bachelors), GetUSSchool()));
                }
                l.Add(new EducationProfile.Degree(DegreeLevel.Masters,
                    GetMajor(DegreeLevel.Masters), GetUSSchool(DegreeLevel.Masters)));
            }
            rd = AnimatorRandom.Rand.NextDouble();
            if (rd < r.DoctorateProbability)
            {
                //Check to make sure there's a bachelor's degree
                if (l.Count == 0 || (l.Count == 1 && l[0].Level != DegreeLevel.Bachelors))
                {
                    l.Add(new EducationProfile.Degree(DegreeLevel.Bachelors,
                        GetMajor(DegreeLevel.Bachelors), GetUSSchool()));
                }
                l.Add(new EducationProfile.Degree(DegreeLevel.Doctorate,
                    GetMajor(DegreeLevel.Doctorate), GetUSSchool(DegreeLevel.Doctorate)));
            }
            rd = AnimatorRandom.Rand.NextDouble();
            if (rd < r.ProfessionalProbability)
            {
                //Check to make sure there's a bachelor's degree
                if (l.Count == 0 || (l.Count == 1 && l[0].Level != DegreeLevel.Bachelors))
                {
                    l.Add(new EducationProfile.Degree(DegreeLevel.Bachelors,
                        GetMajor(DegreeLevel.Bachelors), GetUSSchool()));
                }
                var t = GetMajor(DegreeLevel.Professional);
                l.Add(new EducationProfile.Degree(DegreeLevel.Professional,
                    t, GetUSSchool(DegreeLevel.Professional, t.Split(',')[1])));
            }
            if (l.Count == 0)
            {
                var wasAdded = false;
                if (Npc.NpcProfile != null)
                {
                    foreach (var address in Npc.NpcProfile.Address)
                    {
                        l.Add(new EducationProfile.Degree(DegreeLevel.HSDiploma, "", new School { Name = $"{address.City} High School", Location = $"{address.City}, USA" }));
                        wasAdded = true;
                        break;
                    }
                }
                if (!wasAdded)
                    l.Add(new EducationProfile.Degree(DegreeLevel.HSDiploma, "", new School()));
            }
            return l;
        }

        public static DegreeLevel GetDegreeLevel()
        {
            var o = Enum.GetValues(typeof(DegreeLevel)).Cast<DegreeLevel>().ToList();
            return o.RandomElement();
        }

        public static string GetMajor(DegreeLevel level)
        {
            MajorDegreeLevel mdl = null;
            if (level == DegreeLevel.None || level == DegreeLevel.GED || level == DegreeLevel.HSDiploma)
            {
                return "";
            }

            var input = File.ReadAllText("config/majors.json");
            var majors = JsonConvert.DeserializeObject<MajorManager>(input, new JsonSerializerSettings { FloatParseHandling = FloatParseHandling.Double });
            string txt;
            switch (level)
            {
                case DegreeLevel.Associates:
                    txt = "Associates";
                    break;
                case DegreeLevel.Bachelors:
                    txt = "Bachelors";
                    break;
                case DegreeLevel.Masters:
                    txt = "Masters";
                    break;
                case DegreeLevel.Doctorate:
                    txt = "Doctorate";
                    break;
                default:
                    txt = "Professional";
                    break;
            }

            foreach (var i in majors.MajorDegreeLevels)
            {
                if (i.Level == txt)
                {
                    mdl = i;
                }
            }

            //Get Random Field
            var rd = AnimatorRandom.Rand.NextDouble() * 100;
            double temp = 0;
            Field field = null;
            foreach (var f in mdl.Fields)
            {
                temp += f.Percent;
                if (rd <= temp)
                {
                    field = f;
                    break;
                }
            }
            //Get random item from Major within Field
            var rd2 = AnimatorRandom.Rand.Next(field.Majors.Count());
            return field.Majors[rd2] + "," + field.DegreeType; //return Major, and degreetype associated with the major
        }

        public static School GetSchool(DegreeLevel level = DegreeLevel.Bachelors, string degreeType = "")
        {
            var input = File.ReadAllText("config/universities.json");
            var o = JsonConvert.DeserializeObject<UniversityManager>(input);
            IList<School> l = null;
            if (level == DegreeLevel.GED || level == DegreeLevel.HSDiploma)
            {
                if (Npc.NpcProfile != null)
                {
                    foreach (var address in Npc.NpcProfile.Address)
                    {
                        return new School { Name = $"{address.City} High School", Location = $"{address.City}, USA" };
                    }
                }
                return new School();
            }

            string type;
            if (level == DegreeLevel.Associates)
            {
                type = "University";
                var rd = AnimatorRandom.Rand.NextDouble();
                if (rd < .9) //community college, else associates degree from a 4 year college
                {
                    var name = $"{Address.GetCounty()} Community College";
                    var s = new School
                    {
                        Name = name,
                        Location = "USA"
                    };
                    return s;
                }
            }
            else if (level == DegreeLevel.Bachelors || level == DegreeLevel.Doctorate || level == DegreeLevel.Masters)
            {
                type = "University";
            }
            else //professional degree
            {
                if (degreeType == "M.D.")
                {
                    type = "Medical School";
                }
                else if (degreeType == "J.D.")
                {
                    type = "Law School";
                }
                else
                {
                    type = "University";
                }
            }
            foreach (var t in o.UniversityTypes)
            {
                if (t.Type == type)
                {
                    l = t.Schools;
                }
            }
            //JSON missing lists for med schools and law schools
            return l.RandomElement();
        }

        public static School GetUSSchool(DegreeLevel level = DegreeLevel.Bachelors, string degreeType = "")
        {
            var input = File.ReadAllText("config/universities.json");
            var o = JsonConvert.DeserializeObject<UniversityManager>(input);
            IList<School> l = null;
            if (level == DegreeLevel.GED || level == DegreeLevel.HSDiploma)
            {
                if (Npc.NpcProfile != null)
                {
                    foreach (var address in Npc.NpcProfile.Address)
                    {
                        return new School { Name = $"{address.City} High School", Location = $"{address.City}, USA" };
                    }
                }
                return new School();
            }

            string type;
            if (level == DegreeLevel.Associates)
            {
                type = "University";
                var rd = AnimatorRandom.Rand.NextDouble();
                if (rd < .9) //community college, else associates degree from a 4 year college
                {
                    var name = $"{Address.GetCounty()} Community College";
                    var location = "USA";
                    School s = new School
                    {
                        Name = name,
                        Location = location
                    };
                    return s;
                }
            }
            else if (level == DegreeLevel.Bachelors || level == DegreeLevel.Doctorate || level == DegreeLevel.Masters)
            {
                type = "University";
            }
            else //professional degree
            {
                if (degreeType == "M.D.")
                {
                    type = "Medical School";
                }
                else if (degreeType == "J.D.")
                {
                    type = "Law School";
                }
                else
                {
                    type = "University";
                }
            }
            foreach (var t in o.UniversityTypes)
            {
                if (t.Type == type)
                {
                    l = t.Schools;
                }
            }
            var tmp = l.RandomElement();
            while (tmp.Location != "USA") //Get a US specific school
            {
                tmp = l.RandomElement();
            }
            return tmp;
        }

        /*
         //Depricated

        public static string GetDegree(DegreeLevel level)
        {
            if(level == DegreeLevel.None)
            {
                return "None";
            }
            if(level == DegreeLevel.GED)
            {
                return "GED";
            }
            if(level == DegreeLevel.HSDiploma)
            {
                return "High School Diploma";
            }
            if(level == DegreeLevel.Associates)
            {
                var index = AnimatorRandom.Rand.Next(ASSOCIATES_TYPES.Length);
                return ASSOCIATES_TYPES[index];
            }
            if(level == DegreeLevel.Bachelors)
            {
                var index = AnimatorRandom.Rand.Next(BACHELORS_TYPES.Length);
                return BACHELORS_TYPES[index];
            }
            if(level == DegreeLevel.Masters)
            {
                var index = AnimatorRandom.Rand.Next(MASTERS_TYPES.Length);
                return MASTERS_TYPES[index];
            }
            if(level == DegreeLevel.Professional)
            {
                var index = AnimatorRandom.Rand.Next(PROFESSIONAL_TYPES.Length);
                return PROFESSIONAL_TYPES[index];
            }
            else
            {
                var index = AnimatorRandom.Rand.Next(DOCTORATE_TYPES.Length);
                return DOCTORATE_TYPES[index];
            }
        }

        public static string GetDegreeShort()
        {
            return $"{DEGREE_SHORT_PREFIX.RandomElement()}";
        }

        public static string GetSchoolName()
        {
            return SCHOOL_PREFIX.RandomElement() + SCHOOL_SUFFIX.RandomElement();
        }

        public static string GetSchoolGenericName()
        {
            switch (AnimatorRandom.Rand.Next(2))
            {
                case 0: return Address.GetUSStateName();
                default: return GetSchoolName();
            }
        }

        
        public static string GetSchool()
        {
            switch (AnimatorRandom.Rand.Next(5))
            {
                case 0:
                case 1: return $"{GetSchoolName()} {SCHOOL_TYPE.RandomElement()}";
                case 2: return $"{GetSchoolGenericName()} {SCHOOL_ADJ.RandomElement()} {SCHOOL_TYPE.RandomElement()}";
                case 3: return $"{SCHOOL_UNI.RandomElement()} of {GetSchoolGenericName()}";
                default: return $"{GetSchoolGenericName()} {SCHOOL_TYPE.RandomElement()} of {MAJOR_NOUN.RandomElement()}";
            }
        }
        

        private static readonly string[] ASSOCIATES_TYPES =
            {"A.A.", "A.S.", "A.E.", "A.G."};

        private static readonly string[] BACHELORS_TYPES =
            {"B.S.", "B.A.", "B.B.A."};

        private static readonly string[] MASTERS_TYPES =
            {"M.A.", "M.B.A.", "M.S."};

        private static readonly string[] DOCTORATE_TYPES =
            {"Ph.D."};

        private static readonly string[] PROFESSIONAL_TYPES =
            {"M.D., J.D."};

        private static readonly string[] DEGREE_SHORT_PREFIX = {"AB", "BS", "BSc", "MA", "MD", "DMus", "DPhil", "PhD"};

        private static readonly string[] DEGREE_PREFIX =
            {"Bachelor of Science", "Bachelor of Arts", "Master of Arts", "Doctor of Medicine", "Bachelor of Music", "Doctor of Philosophy"};

        private static readonly string[] MAJOR_ADJ =
            {"Business", "Systems", "Industrial", "Medical", "Financial", "Marketing", "Political", "Social", "Human", "Resource"};

        private static readonly string[] MAJOR_NOUN =
        {
            "Science", "Arts", "Administration", "Engineering", "Management", "Production", "Economics", "Architecture", "Accountancy", "Education",
            "Development", "Philosophy", "Studies"
        };

        private static readonly string[] SCHOOL_PREFIX = {"Green", "South", "North", "Wind", "Lake", "Hill", "Lark", "River", "Red", "White"};

        private static readonly string[] SCHOOL_SUFFIX =
            {"wood", "dale", "ridge", "ville", "point", "field", "shire", "shore", "crest", "spur", "well", "side", "coast"};

        private static readonly string[] SCHOOL_ADJ = {"International", "Global", "Polytechnic", "National"};
        private static readonly string[] SCHOOL_TYPE = {"School", "University", "College", "Institution", "Academy"};
        private static readonly string[] SCHOOL_UNI = {"University", "College"};
    */

    }
}
