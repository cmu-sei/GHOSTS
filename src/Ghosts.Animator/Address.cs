// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ghosts.Animator.Extensions;
using Ghosts.Animator.Models;
using Newtonsoft.Json;

namespace Ghosts.Animator
{
    public static class Address
    {
        static Address()
        {
            //CITY_PREFIXES = _CITY_PREFIXES.SelectMany(item => COMPASS_DIRECTIONS.Select(dir => dir + " " + item)).ToArray();
            var raw = File.ReadAllText("config/us_population_data.json");
            US_POPULATION_DATA = JsonConvert.DeserializeObject<USPopulationData>(raw);
        }


        public static string GetUSStateAbbreviation()
        {
            var states = US_POPULATION_DATA.States;
            var totalPop = US_POPULATION_DATA.TotalPopulation;
            var distributionChoice = AnimatorRandom.Rand.Next(totalPop);

            var runningTotal = 0;
            var selectedState = "";

            foreach (var currState in states)
            {
                if (runningTotal + currState.Population >= distributionChoice)
                {
                    selectedState = currState.Abbreviation
                        ;
                    break;
                }
                else
                {
                    runningTotal += currState.Population;
                }
            }

            return selectedState;

        }

        public static string GetUSStateName()
        {
            var states = US_POPULATION_DATA.States;
            var totalPop = US_POPULATION_DATA.TotalPopulation;
            var distributionChoice = AnimatorRandom.Rand.Next(totalPop);

            var runningTotal = 0;
            var selectedState = "";

            foreach (var currState in states)
            {
                if (runningTotal + currState.Population >= distributionChoice)
                {
                    selectedState = currState.Name;
                    break;
                }
                else
                {
                    runningTotal += currState.Population;
                }
            }

            return selectedState;

        }

        public static string GetCity()
        {
            var popData = US_POPULATION_DATA;
            var totalPopulation = popData.TotalPopulation;
            var distributionChoice = AnimatorRandom.Rand.Next(totalPopulation);
            var selectedCity = "";

            var runningTotal = 0;
            foreach (var currState in popData.States)
            {
                var currStatePop = currState.Population;
                if (runningTotal + currStatePop >= distributionChoice)
                {

                    foreach (var currCity in currState.Cities)
                    {
                        var currCityPop = currCity.Population;
                        if (runningTotal + currCityPop >= distributionChoice)
                        {
                            selectedCity = currCity.Name;
                            break;
                        }
                        else
                        {
                            runningTotal += currCityPop;
                        }
                    }

                    break;
                }
                else
                {
                    runningTotal += currStatePop;
                }
            }
            return selectedCity;
        }

        public static string GetCounty()
        {
            var popData = US_POPULATION_DATA;
            var totalPopulation = popData.TotalPopulation;
            var distributionChoice = AnimatorRandom.Rand.Next(totalPopulation);
            var selectedCounty = "";

            var runningTotal = 0;
            foreach (var currState in popData.States)
            {
                var currStatePop = currState.Population;
                if (runningTotal + currStatePop >= distributionChoice)
                {

                    foreach (var currCity in currState.Cities)
                    {
                        var currCityPop = currCity.Population;
                        if (runningTotal + currCityPop >= distributionChoice)
                        {
                            selectedCounty = currCity.County;
                            break;
                        }
                        else
                        {
                            runningTotal += currCityPop;
                        }
                    }

                    break;
                }
                else
                {
                    runningTotal += currStatePop;
                }
            }
            return selectedCounty;
        }

        public static string GetCityFromStateAbbreviation(string abbrev)
        {
            var statesList = US_POPULATION_DATA.States;
            var selectedCity = "Default City";

            foreach (var state in statesList)
            {
                if (state.Abbreviation.Equals(abbrev))
                {
                    var statePop = state.Population;
                    var distributionChoice = AnimatorRandom.Rand.Next(statePop);
                    var runningTotal = 0;

                    foreach (var city in state.Cities)
                    {
                        if (runningTotal + city.Population >= distributionChoice)
                        {
                            selectedCity = city.Name;
                            break;
                        }
                        else
                        {
                            runningTotal += city.Population;
                        }
                    }
                }
            }
            return selectedCity;
        }

        public static Dictionary<string, string> GetCityAndZipFromStateAbbreviation(string abbrev)
        {
            var statesList = US_POPULATION_DATA.States;
            var selectedCity = "Default City";
            var selectedZipCode = "00000";

            foreach (var state in statesList)
            {
                if (state.Abbreviation.Equals(abbrev))
                {
                    var statePop = state.Population;
                    var distributionChoice = AnimatorRandom.Rand.Next(statePop);
                    var runningTotal = 0;

                    foreach (var city in state.Cities)
                    {
                        if (runningTotal + city.Population >= distributionChoice)
                        {
                            selectedCity = city.Name;
                            foreach (var zipCode in city.ZipCodes)
                            {
                                if (runningTotal + zipCode.Population >= distributionChoice)
                                {
                                    selectedZipCode = zipCode.Id;
                                    break;
                                }
                                else
                                {
                                    runningTotal += zipCode.Population;
                                }
                            }
                            break;
                        }
                        else
                        {
                            runningTotal += city.Population;
                        }
                    }
                }
            }

            var addressData = new Dictionary<string, string> {
                {"City", selectedCity},
                {"ZipCode", selectedZipCode}
            };
            return addressData;
        }

        public static Dictionary<string, string> GetFullZipCodeInfo()
        {
            var popData = US_POPULATION_DATA;
            var totalPopulation = popData.TotalPopulation;
            var distributionChoice = AnimatorRandom.Rand.Next(totalPopulation);

            var selectedState = "";
            var selectedCity = "";
            var selectedCounty = "";
            var selectedTimeZone = "";
            var selectedZipCode = "";

            var runningTotal = 0;
            foreach (var currState in popData.States)
            {
                var currStatePop = currState.Population;
                if (runningTotal + currStatePop >= distributionChoice)
                {
                    selectedState = currState.Name;

                    foreach (var currCity in currState.Cities)
                    {
                        var currCityPop = currCity.Population;
                        if (runningTotal + currCityPop >= distributionChoice)
                        {
                            selectedCity = currCity.Name;
                            selectedCounty = currCity.County;
                            selectedTimeZone = currCity.Timezone;

                            foreach (var currZipCode in currCity.ZipCodes)
                            {
                                var currZipPop = currZipCode.Population;
                                if (runningTotal + currZipPop >= distributionChoice)
                                {

                                    selectedZipCode = currZipCode.Id;
                                    break;
                                }
                                else
                                {
                                    runningTotal += currZipPop;
                                }
                            }

                            break;
                        }
                        else
                        {
                            runningTotal += currCityPop;
                        }
                    }

                    break;
                }
                else
                {
                    runningTotal += currStatePop;
                }
            }

            var zipData = new Dictionary<string, string> {
                {"State", selectedState},
                {"City", selectedCity},
                {"County", selectedCounty},
                {"TimeZone", selectedTimeZone},
                {"ZipCode", selectedZipCode}
            };

            return zipData;

        }

        public static string GetStreetSuffix()
        {
            return STREET_SUFFIXES.RandomElement();
        }

        public static string GetStreetName()
        {
            switch (AnimatorRandom.Rand.Next(2))
            {
                case 0: return Name.GetLastName() + " " + GetStreetSuffix();
                case 1: return Name.GetFirstName() + " " + GetStreetSuffix();
                default: throw new ApplicationException();
            }
        }

        public static AddressProfiles.AddressProfile GetHomeAddress()
        {
            var fullZipData = Address.GetFullZipCodeInfo();

            var a = new AddressProfiles.AddressProfile
            {
                AddressType = "Home",
                Address1 = GetStreetAddress(),
                City = fullZipData["City"],
                State = fullZipData["State"],
                PostalCode = fullZipData["ZipCode"]
            };
            return a;
        }

        public static string GetStreetAddress(bool includeSecondary = false)
        {
            var s = $"{"###".Numerify()} {GetStreetName()}";
            if (includeSecondary)
                s += " " + GetSecondaryAddress();

            return s.Numerify();
        }

        public static string GetSecondaryAddress()
        {
            return SEC_ADDRESSES.RandomElement().Numerify();
        }

        // UK Variants
        public static string GetUKCounty()
        {
            return UK_COUNTIES.RandomElement();
        }

        public static string GetUKCountry()
        {
            return UK_COUNTRIES.RandomElement();
        }

        public static string GetUKPostcode()
        {
            return UK_POSTCODES.RandomElement().Bothify().ToUpper();
        }

        public static string GetNeighborhood()
        {
            return NEIGHBORHOODS.RandomElement();
        }

        public static string GetCountry()
        {
            var o = LoadData();
            return o.RandomElement().Country;
        }

        public static AddressProfiles.InternationalAddressProfile GetInternationalAddress(string country = "")
        {
            var o = LoadData(country);
            return o.RandomElement();
        }

        public static string GetCountryCode(string country)
        {
            var result = new FileInfo(@"config/countries.txt").ReadAndFilter(s => s.Contains(country));
            foreach (var r in result)
            {
                return r.Split(Convert.ToChar("|"))[0];
            }
            return string.Empty;
        }

        private static IEnumerable<AddressProfiles.InternationalAddressProfile> LoadData(string country = "")
        {
            var raw = File.ReadAllText("config/address_international_cities.json");
            var o = JsonConvert.DeserializeObject<IEnumerable<AddressProfiles.InternationalAddressProfile>>(raw);
            return !string.IsNullOrEmpty(country) ? o.Where(x => x.Country.Equals(country, StringComparison.CurrentCultureIgnoreCase)) : o;
        }

        // private static readonly string[] COMPASS_DIRECTIONS = {"North", "East", "West", "South"};
        // private static readonly string[] _CITY_PREFIXES = {"New", "Lake", "Port", "Old", "Fort"};
        // private static readonly string[] CITY_PREFIXES;
        // private static readonly string[] CITY_SUFFIXES = {
        //     "town", "ton", "land", "ville", "berg", "burgh", "borough", "bury", "view", "port",
        //     "mouth", "stad", "furt", "chester", "mouth", "fort", "haven", "side", "shire"
        // };

        private static readonly string[] STREET_SUFFIXES = {
            "Alley", "Avenue", "Branch", "Bridge", "Brook", "Brooks", "Burg", "Burgs", "Bypass", "Camp", "Canyon", "Cape", "Causeway", "Center",
            "Centers", "Circle", "Circles", "Cliff", "Cliffs", "Club", "Common", "Corner", "Corners", "Course", "Court", "Courts", "Cove", "Coves",
            "Creek", "Crescent", "Crest", "Crossing", "Crossroad", "Curve", "Dale", "Dam", "Divide", "Drive", "Drives", "Estate", "Estates",
            "Expressway", "Extension", "Extensions", "Fall", "Falls", "Ferry", "Field", "Fields", "Flat", "Flats", "Ford", "Fords", "Forest", "Forge",
            "Forges", "Fork", "Forks", "Fort", "Freeway", "Garden", "Gardens", "Gateway", "Glen", "Glens", "Green", "Greens", "Grove", "Groves",
            "Harbor", "Harbors", "Haven", "Heights", "Highway", "Hill", "Hills", "Hollow", "Inlet", "Island", "Islands", "Isle", "Junction",
            "Junctions", "Key", "Keys", "Knoll", "Knolls", "Lake", "Lakes", "Land", "Landing", "Lane", "Light", "Lights", "Loaf", "Lock", "Locks",
            "Lodge", "Loop", "Mall", "Manor", "Manors", "Meadow", "Meadows", "Mews", "Mill", "Mills", "Mission", "Motorway", "Mount", "Mountain",
            "Mountains", "Neck", "Orchard", "Oval", "Overpass", "Park", "Parks", "Parkway", "Parkways", "Pass", "Passage", "Path", "Pike", "Pine",
            "Pines", "Place", "Plain", "Plains", "Plaza", "Point", "Points", "Port", "Ports", "Prairie", "Radial", "Ramp", "Ranch", "Rapid", "Rapids",
            "Rest", "Ridge", "Ridges", "River", "Road", "Roads", "Route", "Row", "Rue", "Run", "Shoal", "Shoals", "Shore", "Shores", "Skyview",
            "Spring", "Springs", "Spur", "Spurs", "Square", "Squares", "Station", "Stream", "Street", "Streets", "Summit", "Terrace",
            "Tire Hill", "Trace", "Track", "Fawn Hill", "Trail", "Tunnel", "Turnpike", "Underpass", "Union", "Unions", "Valley", "Valleys", "Via",
            "Viaduct", "View", "Views", "Village", "Villages", "Ville", "Vista", "Walk", "Walks", "Wall", "Way", "Ways", "Well", "Wells"
        };

        private static readonly string[] SEC_ADDRESSES = { "Apt. ###", "Suite ###", "Box ###" };

        private static readonly string[] UK_COUNTIES = {
            "Avon", "Bedfordshire", "Berkshire", "Borders",
            "Buckinghamshire", "Cambridgeshire", "Central", "Cheshire", "Cleveland",
            "Clwyd", "Cornwall", "County Antrim", "County Armagh", "County Down",
            "County Fermanagh", "County Londonderry", "County Tyrone", "Cumbria",
            "Derbyshire", "Devon", "Dorset", "Dumfries and Galloway", "Durham",
            "Dyfed", "East Sussex", "Essex", "Fife", "Gloucestershire", "Grampian",
            "Greater Manchester", "Gwent", "Gwynedd County", "Hampshire",
            "Herefordshire", "Hertfordshire", "Highlands and Islands", "Humberside",
            "Isle of Wight", "Kent", "Lancashire", "Leicestershire", "Lincolnshire",
            "Lothian", "Merseyside", "Mid Glamorgan", "Norfolk", "North Yorkshire",
            "Northamptonshire", "Northumberland", "Nottinghamshire", "Oxfordshire",
            "Powys", "Rutland", "Shropshire", "Somerset", "South Glamorgan",
            "South Yorkshire", "Staffordshire", "Strathclyde", "Suffolk", "Surrey",
            "Tayside", "Tyne and Wear", "Warwickshire", "West Glamorgan", "West Midlands",
            "West Sussex", "West Yorkshire", "Wiltshire", "Worcestershire"
        };

        private static readonly string[] UK_COUNTRIES = { "England", "Scotland", "Wales", "Northern Ireland" };

        private static readonly string[] UK_POSTCODES = { "??# #??", "??## #??" };

        private static readonly string[] NEIGHBORHOODS = {
            "East of Telegraph Road", "North Norridge", "Northwest Midlothian/Midlothian Country Club",
            "Mott Haven/Port Morris", "Kingsbridge Heights", "Bronxdale", "Pennypack", "Bridesburg",
            "Allegheny West", "Bushwick South", "Dyker Heights", "Ocean Parkway South", "Summerlin North",
            "Seven Hills Area", "Greater Las Vegas National", "phoenix", "Central Chandler", "South of Bell Road",
            "River Heights", "White Plains Central", "Mount Kisco West", "Pound Ridge East", "Babylon Bayside",
            "Sagaponack Seaside", "South of Lake Ave", "Far Rockaway/Bayswater", "Jamaica Estates/Holliswood",
            "Murray Hill", "East Renton", "Renton West", "Auburn North", "Northwoods West", "Florissant West",
            "Ladue South", "Candlewood Country Club", "West Covina East", "North East Irwindale", "Sunshine-Gardens",
            "Cipriani", "Brentwood Central", "Jupiter South/Abacoa", "Sea Ranch Lakes", "Schall Circle/Lakeside Green",
            "Olmsted Falls Central", "South of Lake Shore Blvd", "Gates Mills North", "White Oak South of Columbia Pike",
            "Rockville East of Hungerford Dr", "Cleveland Park"
        };

        private static readonly USPopulationData US_POPULATION_DATA;

    };
}
