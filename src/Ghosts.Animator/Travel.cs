// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using Ghosts.Animator.Models;

namespace Ghosts.Animator
{
    public static class Travel
    {
        public static IEnumerable<ForeignTravelProfile.Trip> GetTrips()
        {
            var list = new List<ForeignTravelProfile.Trip>();
            for (var i = 0; i < AnimatorRandom.Rand.Next(0, 20); i++)
            {
                list.Add(GetTrip());
            }

            return list;
        }

        public static ForeignTravelProfile.Trip GetTrip()
        {
            var arrive = DateTime.Now.AddYears(AnimatorRandom.Rand.Next(-20, -1)).AddHours(AnimatorRandom.Rand.Next(1, 72))
                .AddMinutes(AnimatorRandom.Rand.Next(1, 60)).AddSeconds(AnimatorRandom.Rand.Next(1, 60));
            var depart = arrive.AddDays(AnimatorRandom.Rand.Next(2, 21)).AddHours(AnimatorRandom.Rand.Next(-24, 24))
                .AddMinutes(AnimatorRandom.Rand.Next(-60, 60)).AddSeconds(AnimatorRandom.Rand.Next(-60, 60));

            var address = Address.GetInternationalAddress();
            var code = Address.GetCountryCode(address.Country);

            return new ForeignTravelProfile.Trip
            { ArriveDestination = arrive, DepartDestination = depart, Destination = address.ToString(), Country = address.Country, Code = code };
        }
    }
}
