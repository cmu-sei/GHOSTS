// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Ghosts.Animator.Enums;
using Ghosts.Animator.Extensions;
using Ghosts.Animator.Models;
using Newtonsoft.Json;

namespace Ghosts.Animator
{
    public static class PhysicalCharacteristics
    {

        public static BiologicalSex GetBiologicalSex()
        {
            var o = Enum.GetValues(typeof(BiologicalSex)).Cast<BiologicalSex>().ToList();
            return o.RandomElement();
        }

        public static BiologicalSex GetMilBiologicalSex()
        {
            var rd = AnimatorRandom.Rand.NextDouble();
            if (rd < .144) //% of active duty women in the military. Should probably export this value to a config.
            {
                return BiologicalSex.Female;
            }
            else
            {
                return BiologicalSex.Male;
            }
        }

        private static int RoundDouble(double x)
        {//Does appropriate mathematical rounding, instead of going to nearest even if val%1==.5
            if (x % 1 < .5)
            {
                return (int)Math.Floor(x);
            }
            else
            {
                return (int)Math.Ceiling(x);
            }
        }
        public static int GetHeight(BiologicalSex sex)
        {
            //Generates height using Box-Muller transform
            double mean;
            double std;
            var u1 = 1.0 - AnimatorRandom.Rand.NextDouble();
            var u2 = 1.0 - AnimatorRandom.Rand.NextDouble();
            var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

            if (sex == BiologicalSex.Male)
            {
                mean = 68.9;
                std = 2.84;
            }
            else
            {
                mean = 63.62;
                std = 2.52;
            }

            var randNormal = mean + std + randStdNormal;
            var inches = RoundDouble(randNormal);
            return inches;
        }
        public static int GetHeight()
        {
            var rd = AnimatorRandom.Rand.Next(2);
            if (rd == 0)
            {
                return GetHeight(BiologicalSex.Female);
            }
            else
            {
                return GetHeight(BiologicalSex.Male);
            }
        }
        public static int GetMilHeight(BiologicalSex sex, MilitaryBranch branch = MilitaryBranch.USARMY)
        {
            double mean;
            double std;
            double u1;
            double u2;
            double randStdNormal;
            double randNormal;
            int inches;

            var low = 60;
            var high = 80;
            if (sex == BiologicalSex.Female)
            {
                low = 58;
                high = 80;
            }
            if (branch == MilitaryBranch.USMC)
            {
                low = 58;
                high = 78;
                if (sex == BiologicalSex.Female)
                {
                    high = 72;
                }
            }

            do
            {
                u1 = 1.0 - AnimatorRandom.Rand.NextDouble();
                u2 = 1.0 - AnimatorRandom.Rand.NextDouble();
                randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

                if (sex == BiologicalSex.Male)
                {
                    mean = 68.9;
                    std = 2.84;
                }
                else
                {
                    mean = 63.62;
                    std = 2.52;
                }

                randNormal = mean + std + randStdNormal;
                inches = RoundDouble(randNormal);
            } while (inches < low || inches > high);

            return inches;
        }
        public static int GetMilHeight(MilitaryBranch branch = MilitaryBranch.USARMY)
        {
            var rv = AnimatorRandom.Rand.Next(2);
            if (rv == 0)
            {
                return GetMilHeight(BiologicalSex.Female, branch);
            }
            else
            {
                return GetMilHeight(BiologicalSex.Male, branch);
            }
        }

        public static string HeightToString(int height)
        {
            return $"{height / 12}' {height % 12}\"";
        }
        /*
         //Depricated
        private static int StrHeightToIntHeight(string height)
        {
            var data = height.Split('\'');
            data[0].Trim();
            data[1].TrimEnd('"');
            int feet = Convert.ToInt32(data[0]);
            int inches = Convert.ToInt32(data[1]);
            return feet * 12 + inches;
        }
        */

        public static int GetWeight(int height, BiologicalSex sex)
        {
            var weightClass = AnimatorRandom.Rand.Next(0, 1000);
            int bmi;
            if (sex == BiologicalSex.Male)
            {
                if (weightClass < 55)
                {
                    //Extreme Obesity (40-54)
                    bmi = AnimatorRandom.Rand.Next(40, 55);
                }
                else if (weightClass < 350)
                {
                    //Obese(30-39)
                    bmi = AnimatorRandom.Rand.Next(30, 40);
                }
                else if (weightClass < 737)
                {
                    //Overweight(25-29)
                    bmi = AnimatorRandom.Rand.Next(25, 30);
                }
                else
                {
                    //Normal Weight(19-24)
                    bmi = AnimatorRandom.Rand.Next(19, 25);
                }
            }
            else
            {
                if (weightClass < 99)
                {
                    bmi = AnimatorRandom.Rand.Next(40, 55);
                }
                else if (weightClass < 404)
                {
                    bmi = AnimatorRandom.Rand.Next(30, 40);
                }
                else if (weightClass < 669)
                {
                    bmi = AnimatorRandom.Rand.Next(25, 30);
                }
                else
                {
                    bmi = AnimatorRandom.Rand.Next(19, 25);
                }
            }

            var input = File.ReadAllText("config/bmi.json");
            var bmiChart = JsonConvert.DeserializeObject<BMIManager>(input);
            double weight;
            var heightList = bmiChart.Heights.FirstOrDefault(x => x.Height == height);
            var selectedBMI = heightList.BMIs.FirstOrDefault(x => x.BMI == bmi);
            if (height <= 76 && height >= 58 &&
                bmiChart.Heights.FirstOrDefault(x => x.Height == height) != null
                && heightList.BMIs.FirstOrDefault(x => x.BMI == bmi) != null)
            {
                //grab from table
                weight = selectedBMI.Weight;
            }
            else
            {
                //BMI = kg/m^2, kg = BMI * m^2
                var metricHeight = height / 39.37;
                weight = bmi * metricHeight * metricHeight;
            }

            //creates a random variance of up to 2 pounds (plus or minus)
            var variation = (AnimatorRandom.Rand.NextDouble() * 2 - 1) * 2;
            var result = RoundDouble(variation + weight);
            return result;
        }
        public static int GetWeight(int height)
        {
            var rd = AnimatorRandom.Rand.Next(2);
            if (rd == 0)
            {
                return GetWeight(height, BiologicalSex.Female);
            }
            else
            {
                return GetWeight(height, BiologicalSex.Male);
            }
        }

        //TODO: Update to reflect weight distribution statistics instead of even distribution within bounds
        public static int GetMilWeight(int height, DateTime birthdate, BiologicalSex sex, MilitaryBranch branch)
        {
            var input = File.ReadAllText("config/military_height_weight.json");
            var hwChart = JsonConvert.DeserializeObject<MilitaryHeightWeight.MilitaryHeightWeightManager>(input);
            var age = DateTime.Now.Year - birthdate.Year;
            if (DateTime.Now.Month < birthdate.Month)
            {
                age -= 1;
            }
            if (DateTime.Now.Month == birthdate.Month && DateTime.Now.Day < birthdate.Day)
            {
                age -= 1;
            }
            int min;
            int max;
            if (branch == MilitaryBranch.USAF)
            {
                var b = hwChart.Branches.FirstOrDefault(x => x.Branch == "USAF");
                var h = b.Heights.FirstOrDefault(x => x.Height == height);
                min = h.MinWeight;
                max = h.MaxWeight;
            }
            else if (branch == MilitaryBranch.USCG)
            {
                var b = hwChart.Branches.FirstOrDefault(x => x.Branch == "USCG");
                var h = b.Heights.FirstOrDefault(x => x.Height == height);
                min = h.MinWeight;
                max = h.MaxWeight;
            }
            else if (branch == MilitaryBranch.USMC)
            {
                var b = hwChart.Branches.FirstOrDefault(x => x.Branch == "USMC");
                if (sex == BiologicalSex.Male)
                {
                    var s = b.Sexes.FirstOrDefault(x => x.Sex == "Male");
                    var h = s.Heights.FirstOrDefault(x => x.Height == height);
                    min = h.MinWeight;
                    max = h.MaxWeight;
                }
                else
                {
                    var s = b.Sexes.FirstOrDefault(x => x.Sex == "Female");
                    var h = s.Heights.FirstOrDefault(x => x.Height == height);
                    min = h.MinWeight;
                    max = h.MaxWeight;
                }
            }
            else if (branch == MilitaryBranch.USN)
            {
                var b = hwChart.Branches.FirstOrDefault(x => x.Branch == "USN");
                if (sex == BiologicalSex.Male)
                {
                    var s = b.Sexes.FirstOrDefault(x => x.Sex == "Male");
                    var h = s.Heights.FirstOrDefault(x => x.Height == height);
                    min = h.MinWeight;
                    max = h.MaxWeight;
                }
                else
                {
                    var s = b.Sexes.FirstOrDefault(x => x.Sex == "Female");
                    var h = s.Heights.FirstOrDefault(x => x.Height == height);
                    min = h.MinWeight;
                    max = h.MaxWeight;
                }
            }
            else
            {
                var b = hwChart.Branches.FirstOrDefault(x => x.Branch == "USARMY");
                if (sex == BiologicalSex.Male)
                {
                    var s = b.Sexes.FirstOrDefault(x => x.Sex == "Male");
                    if (age < 21)
                    {
                        var a = s.Ages.FirstOrDefault(x => x.Age == 17);
                        var h = a.Heights.FirstOrDefault(x => x.Height == height);
                        min = h.MinWeight;
                        max = h.MaxWeight;
                    }
                    else if (age < 28)
                    {
                        var a = s.Ages.FirstOrDefault(x => x.Age == 21);
                        var h = a.Heights.FirstOrDefault(x => x.Height == height);
                        min = h.MinWeight;
                        max = h.MaxWeight;
                    }
                    else if (age < 40)
                    {
                        var a = s.Ages.FirstOrDefault(x => x.Age == 28);
                        var h = a.Heights.FirstOrDefault(x => x.Height == height);
                        min = h.MinWeight;
                        max = h.MaxWeight;
                    }
                    else
                    {
                        var a = s.Ages.FirstOrDefault(x => x.Age == 40);
                        var h = a.Heights.FirstOrDefault(x => x.Height == height);
                        min = h.MinWeight;
                        max = h.MaxWeight;
                    }
                }
                else
                {
                    var s = b.Sexes.FirstOrDefault(x => x.Sex == "Female");
                    if (age < 21)
                    {
                        var a = s.Ages.FirstOrDefault(x => x.Age == 17);
                        var h = a.Heights.FirstOrDefault(x => x.Height == height);
                        min = h.MinWeight;
                        max = h.MaxWeight;
                    }
                    else if (age < 28)
                    {
                        var a = s.Ages.FirstOrDefault(x => x.Age == 21);
                        var h = a.Heights.FirstOrDefault(x => x.Height == height);
                        min = h.MinWeight;
                        max = h.MaxWeight;
                    }
                    else if (age < 40)
                    {
                        var a = s.Ages.FirstOrDefault(x => x.Age == 28);
                        var h = a.Heights.FirstOrDefault(x => x.Height == height);
                        min = h.MinWeight;
                        max = h.MaxWeight;
                    }
                    else
                    {
                        var a = s.Ages.FirstOrDefault(x => x.Age == 40);
                        var h = a.Heights.FirstOrDefault(x => x.Height == height);
                        min = h.MinWeight;
                        max = h.MaxWeight;
                    }
                }
            }
            return CalcMilWeight(min, max);
        }
        public static int GetMilWeight(int height, DateTime birthdate)
        {
            var rv = AnimatorRandom.Rand.Next(2);
            if (rv == 0)
            {
                return GetMilWeight(height, birthdate, BiologicalSex.Female);
            }
            else
            {
                return GetMilWeight(height, birthdate, BiologicalSex.Male);
            }
        }
        public static int GetMilWeight(int height, DateTime birthdate, BiologicalSex sex)
        {
            var rv = AnimatorRandom.Rand.Next(5);
            if (rv == 0)
            {
                return GetMilWeight(height, birthdate, sex, MilitaryBranch.USAF);
            }
            else if (rv == 1)
            {
                return GetMilWeight(height, birthdate, sex, MilitaryBranch.USARMY);
            }
            else if (rv == 2)
            {
                return GetMilWeight(height, birthdate, sex, MilitaryBranch.USCG);
            }
            else if (rv == 3)
            {
                return GetMilWeight(height, birthdate, sex, MilitaryBranch.USMC);
            }
            else
            {
                return GetMilWeight(height, birthdate, sex, MilitaryBranch.USN);
            }
        }
        public static int GetMilWeight(int height, DateTime birthdate, MilitaryBranch branch)
        {
            var rv = AnimatorRandom.Rand.Next(2);
            if (rv == 0)
            {
                return GetMilWeight(height, birthdate, BiologicalSex.Female, branch);
            }
            else
            {
                return GetMilWeight(height, birthdate, BiologicalSex.Male, branch);
            }
        }

        private static int CalcMilWeight(int min, int max)
        {
            var avg = (min + max) / 2;
            var diff = max - avg;
            var mod = (AnimatorRandom.Rand.NextDouble() * 2 - 1) * diff;
            return RoundDouble(avg + mod);
        }

        public static string WeightToString(int weight)
        {
            return $"{weight} lbs";
        }

        public static string GetBloodType()
        {
            return new Dictionary<string, double>
            {
                {"O+", 37},
                {"A+", 36},
                {"B+", 9},
                {"AB+", 3},
                {"O-", 7},
                {"A-", 6},
                {"B-", 2},
                {"AB-", 1}
            }.RandomFromProbabilityList();
        }

        public static DateTime GetBirthdate(string rankPayGrade = "2")
        {
            const int low = 17;
            const int high = 37;
            var ageInDays = AnimatorRandom.Rand.Next(low, high) * 365;

            var r = Convert.ToInt32(Regex.Replace(rankPayGrade, @"[^\d]", "")) * 365;

            ageInDays = Convert.ToInt32(ageInDays + (r + AnimatorRandom.Rand.NextDouble()));
            return DateTime.Now.AddDays(-ageInDays).AddHours(AnimatorRandom.Rand.Next(-1000, 1000));
        }


        public static string GetPhotoUrl()
        {
            var dir = "config/photos/";
            if (Npc.NpcProfile != null)
            {
                dir += Npc.NpcProfile.BiologicalSex.ToString().ToLower();
            }
            else
            {
                dir += GetBiologicalSex().ToString().ToLower();
            }
            dir = Directory.GetDirectories(dir).RandomElement();
            var file = Directory.GetFiles(dir).RandomElement();

            return file;
        }

        /*
         //Old GetWeight function
     public static int GetWeight()
     {
         var low = 110;
         var high = 225;
         if (Npc.NpcProfile != null)
         {
             if (Npc.NpcProfile.BiologicalSex == BiologicalSex.Female)
             {
                 low = 100;
                 high = 170;
             }

             if (!string.IsNullOrEmpty(Npc.NpcProfile.Height))
             {
                 var o = Npc.NpcProfile.Height.Split(Convert.ToChar("'"));
                 var total = (Convert.ToInt32(o[0]) * 12) + Convert.ToInt32(o[1].Replace("\"", ""));
                 if (total < 62)
                 {
                     low = 100;
                     high = 150;
                 }
             }
         }


         return AnimatorRandom.Rand.Next(low, high);
     }
     */

    }
}
