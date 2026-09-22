// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class SpeedEvaluator
    {
        /// <summary>
        /// Evaluates the difficulty of tapping the current note, based on:
        /// <list type="bullet">
        /// <item><description>how long it has been since the previous note,</description></item>
        /// <item><description>whether that note was in the same column,</description></item>
        /// <item><description>and how easily the pattern around it can be cheesed.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject hitObject)
        {
            // Every note of a chord is pressed at once, so only the first of them costs a tap.
            if (hitObject.DeltaTime < ChordUtils.CHORD_TOLERANCE_MS)
                return 0.0;

            const double tap_rate_offset_ms = 30;
            const double speed_weight = 1.983;

            // A repeat in the same column is one finger doing the work of two, so it taps slower than its gap suggests.
            const double jack_speed_nerf = 0.49996;

            // Total combines the tap skills in quadrature, so this evaluator carries the square root of its weight.
            const double total_weight = 1.01112; // sqrt(1.02237)

            double tapRate = 1000.0 / (hitObject.DeltaTime + tap_rate_offset_ms);
            tapRate *= speedGrowth(hitObject);

            bool isJack = hitObject.Previous() is ManiaDifficultyHitObject previous && previous.Column == hitObject.Column;

            double patternMultiplier = isJack ? jack_speed_nerf : TrillUtils.TrillFactor(hitObject);

            return tapRate * patternMultiplier * speed_weight * hitObject.ManipulationFactor * hitObject.EnduranceFactor * total_weight;
        }

        /// <summary>
        /// Soften growth because a short speed spike does not demand as much as a sustained difficulty.
        /// </summary>
        private static double speedGrowth(ManiaDifficultyHitObject hitObject)
        {
            int rows = 0, shared = 0;
            for (var p = hitObject.Row.Previous(); p != null && hitObject.StartTime - p.StartTime <= 400.0; p = p.Previous())
            {
                rows++;
                if (ColumnPatternUtils.SharesColumn(hitObject.Row.Columns, p.Columns)) shared++;
            }

            if (rows < 4)
                return 1.0;

            double density = DiffUtils.Smoothstep(rows, 5.0, 8.0);
            double even = 1.0 - (double)shared / rows;
            return 1.0 - 0.425 * density * DiffUtils.Smoothstep(even, 0.55, 0.85);
        }
    }
}
