// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

namespace Ghosts.Domain.Code.Helpers
{
    public static class NumberExtensions
    {
        public static bool IsDivisibleByN(this double n, int divisibleBy)
        {
            return n % divisibleBy == 0;
        }

        public static bool IsDivisibleByN(this int n, int divisibleBy)
        {
            return n % divisibleBy == 0;
        }
    }
}
