// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    /// <summary>
    /// A curve of the formula skill/difficulty = 1 - (x/maxUr)^scaling
    /// Can also be rewritten as skill = difficulty * (1 - (x/maxUr)^scaling)
    /// which is what is used to derive what the skill level is in ManiaSkill.
    /// </summary>
    public readonly struct NoteAccuracyCurve
    {
        public const double MAX_UNSTABLE_RATE = 400;
        public required double Difficulty { get; init; }
        public required double ScaleFactor { get; init; }

        /// <summary>
        /// Returns the combination of the curves, where difficulty of each curve is lp normed
        /// and the scales of the curve are averaged arithmetically, weighed by each difficulty.
        /// </summary>
        /// <param name="p">The Lp norm factor.</param>
        /// <param name="curves">The entries to add.</param>
        /// <returns>The scaled curve.</returns>
        public static NoteAccuracyCurve PNormSum(double p, params NoteAccuracyCurve[] curves)
        {
            double max = curves.Max(x => x.Difficulty);

            if (max <= 0)
            {
                return new NoteAccuracyCurve
                {
                    Difficulty = 0,
                    ScaleFactor = 0
                };
            }

            double massSum = 0;
            double weightedScale = 0;

            foreach (var c in curves)
            {
                double mass = Math.Pow(c.Difficulty / max, p);
                massSum += mass;
                weightedScale += mass * c.ScaleFactor;
            }

            return new NoteAccuracyCurve
            {
                Difficulty = max * Math.Pow(massSum, 1.0 / p),
                ScaleFactor = weightedScale / massSum
            };
        }
    }
}
