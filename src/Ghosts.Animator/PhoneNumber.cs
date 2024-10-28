// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Animator.Extensions;

namespace Ghosts.Animator
{
    public static class PhoneNumber
    {
        public static string GetPhoneNumber()
        {
            return FormatPhoneNumber().Numerify();
        }

        private static string FormatPhoneNumber()
        {
            return "(###) ###-####".Numerify();
        }
    }
}
