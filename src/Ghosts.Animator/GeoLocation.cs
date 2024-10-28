// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using Ghosts.Animator.Extensions;

namespace Ghosts.Animator
{
    public static class GeoLocation
    {
        public static double GetLat()
        {
            return LATLNG.RandomElement().Item1;
        }

        public static double GetLng()
        {
            return LATLNG.RandomElement().Item2;
        }

        static readonly Tuple<double, double>[] LATLNG = new[]
        {
            Tuple.Create(41.022921, -73.667595),
            Tuple.Create(41.017643, -73.769264),
            Tuple.Create(40.89505, -73.83582),
            Tuple.Create(41.018461, -73.66616),
            Tuple.Create(40.6665875097692, -73.8830092325694),
            Tuple.Create(40.6784867492198, -73.9716387729985),
            Tuple.Create(40.5998084142575, -73.9385565185842),
            Tuple.Create(40.6175673160943, -74.0002511295821),
            Tuple.Create(40.7451545655034, -73.971855164776),
            Tuple.Create(40.7143394939935, -73.9783896644132),
            Tuple.Create(40.7916523500249, -73.9693478828185),
            Tuple.Create(40.8005590858048, -73.9473684707957),
            Tuple.Create(40.7292057951864, -73.7555431517841),
            Tuple.Create(40.7532199291845, -73.8842977517553),
            Tuple.Create(40.7490546706085, -73.9187706945134),
            Tuple.Create(40.6609944585817, -73.8454648940358),
            Tuple.Create(38.9962071932467, -77.0898681997577),
            Tuple.Create(38.9614005154765, -77.0739996811784),
            Tuple.Create(38.9381545320739, -77.0656875352079),
            Tuple.Create(39.0788984258721, -77.0500853418371),
            Tuple.Create(40.8377346033077, -73.8618025934729),
            Tuple.Create(40.8377346033077, -73.8618025934729),
            Tuple.Create(40.8831107806302, -73.9212453413708),
            Tuple.Create(40.9077802515069, -73.9025768857695),
            Tuple.Create(36.1936372347, -115.068968492),
            Tuple.Create(36.2345447488, -115.327274645),
            Tuple.Create(36.1257785585, -115.08848819),
            Tuple.Create(36.0150030591, -115.120716573),
            Tuple.Create(41.5205, -81.587),
            Tuple.Create(41.491529, -81.611008),
            Tuple.Create(41.4811, -81.9136),
            Tuple.Create(41.5244, -81.5531),
            Tuple.Create(38.5241394042969, -90.3121643066406),
            Tuple.Create(38.4685363769531, -90.3760452270508),
            Tuple.Create(38.7077140808105, -90.2698593139648),
            Tuple.Create(38.7953453063965, -90.2058792114258),
            Tuple.Create(33.5775667696072, -112.21954995412),
            Tuple.Create(33.4136301699617, -112.605812600303),
            Tuple.Create(33.3732084677927, -111.602125919385),
            Tuple.Create(33.4280828890754, -112.496547310057),
            Tuple.Create(40.0677174866262, -75.0764604391247),
            Tuple.Create(39.9760149034563, -75.1786003814711),
            Tuple.Create(39.9871631180866, -75.1862338204704),
            Tuple.Create(39.9847861520773, -75.110396933127),
            Tuple.Create(26.1180992126465, -80.149299621582),
            Tuple.Create(25.4804286236799, -80.4256357381565),
            Tuple.Create(26.1793003082275, -80.1410980224609),
            Tuple.Create(26.5322723388672, -80.1300048828125),
            Tuple.Create(47.685714288367, -122.340967372417),
            Tuple.Create(47.6993431274679, -122.395610510952),
            Tuple.Create(47.7553943974153, -122.305764516646),
            Tuple.Create(47.5173276931226, -122.275152683751),
            Tuple.Create(40.78595, -73.196244),
            Tuple.Create(40.927955, -73.048076),
            Tuple.Create(41.022872, -72.204989),
            Tuple.Create(40.855153, -72.572405),
            Tuple.Create(34.101011924908, -118.064638782714),
            Tuple.Create(34.2430955947492, -118.427610513239),
            Tuple.Create(34.3823767402857, -118.550562688364),
            Tuple.Create(33.8256050190507, -118.281161297494),
            Tuple.Create(37.5758033375583, -122.012044535507),
            Tuple.Create(37.8768587606888, -122.078250641083),
            Tuple.Create(37.6859990796181, -122.094516147761),
            Tuple.Create(37.4660979087165, -121.900873639257),
            Tuple.Create(41.77117, -87.888795),
            Tuple.Create(41.900425, -87.624262),
            Tuple.Create(41.737173, -87.869998)
        };
    }
}
