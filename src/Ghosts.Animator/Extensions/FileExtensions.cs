// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.IO;
using System.Linq;

namespace Ghosts.Animator.Extensions
{
    public static class FileExtensions
    {
        public static string GetRandomFromFile(this string file)
        {
            var f = File.ReadLines(file).ToList();
            var count = f.Count;
            var line = f.Skip(AnimatorRandom.Rand.Next(0, count)).First();
            return line.Trim();
        }
    }
}
