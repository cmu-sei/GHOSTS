// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Drawing;

namespace Ghosts.Domain.Code.Helpers
{
    public static class StylingExtensions
    {
        public static Color GetRandomColor()
        {
            var random = new Random();
            return Color.FromArgb((byte)random.Next(0, 255), (byte)random.Next(0, 255), (byte)random.Next(0, 255));
        }
    }
}
