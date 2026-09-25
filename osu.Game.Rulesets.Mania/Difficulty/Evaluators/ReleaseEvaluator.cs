// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.
using System;
using System.Linq;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class ReleaseEvaluator
    {
        /// <summary>
        /// Holds longer than this are all the same amount of work to keep held, so the duration is capped here
        /// before anything reads it.
        /// </summary>
        private const double max_long_note_duration_ms = 1000.0;

        /// <summary>
        /// Evaluates the difficulty of letting go of the current long note, based on:
        /// <list type="bullet">
        /// <item><description>how long it is held for,</description></item>
        /// <item><description>how close its release lands to a release in another column,</description></item>
        /// <item><description>and how many other long notes are held during its release.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject current)
        {
            double releaseDifficulty = 0.0;

            if (current.BaseObject is not HoldNote)
                return releaseDifficulty;

            // Release is not combined in quadrature with the tap skills, so it carries its whole weight here.
            const double total_weight = 2.83449;

            double duration = Math.Min(current.EndTime - current.StartTime, max_long_note_duration_ms);
            double longNoteGate = longNoteGateOf(duration);

            releaseDifficulty += calculateLongHoldBonus(duration, longNoteGate);
            releaseDifficulty += calculateReleaseSpeedBonus(current, longNoteGate);
            releaseDifficulty += calculateReleaseWhileHolds(current, longNoteGate);

            return releaseDifficulty * total_weight;
        }

        /// <summary>
        /// How much of a hold note this object really is. A hold barely longer than a tap ends in the same
        /// motion that started it, so short durations fade out rather than switching off at a hard length.
        /// </summary>
        private static double longNoteGateOf(double duration) => DiffUtils.Logistic(duration, 110.90068, 0.07);

        /// <summary>
        /// The work of holding the note down, growing with its duration and growing faster once the hold is long
        /// enough that it has to be tracked rather than just ridden out.
        /// </summary>
        private static double calculateLongHoldBonus(double duration, double longNoteGate)
        {
            double seconds = duration / 1000.0;
            double holdLengthFactor = 1.6 * DiffUtils.Smoothstep(duration, 500, 680) * seconds;

            return (0.42 + 0.9 * seconds + holdLengthFactor) * longNoteGate;
        }

        /// <summary>
        /// Releases very close together are harder to time apart, so the closest release in any other column that
        /// is still being held is paid for here.
        /// </summary>
        private static double calculateReleaseSpeedBonus(ManiaDifficultyHitObject current, double longNoteGate)
        {
            const double slope = 0.1;
            const double offset_ms = 30.0;
            const double weight = 0.2;

            double closestReleaseDelta = double.PositiveInfinity;

            foreach (double? endTime in current.LastConcurrentlyReleasedHolds.Select(o => o?.EndTime))
            {
                if (endTime is null || Math.Abs(endTime.Value - current.EndTime) <= ChordUtils.CHORD_TOLERANCE_MS)
                    continue;

                closestReleaseDelta = Math.Min(closestReleaseDelta, Math.Abs(current.EndTime - endTime.Value));
            }

            if (double.IsPositiveInfinity(closestReleaseDelta))
                return 0.0;

            return weight * DiffUtils.Logistic(slope * (closestReleaseDelta - offset_ms), longNoteGate);
        }

        /// <summary>
        /// Other held notes makes the movement of a release harder, scaling with how many columns are held, and
        /// nerfed if the next note in this column follows too closely.
        /// </summary>
        private static double calculateReleaseWhileHolds(ManiaDifficultyHitObject current, double longNoteGate)
        {
            const double release_long_note_weight = 0.4;

            int releasingColumns = current.TailOverlappedHolds.Count(o => o is not null);

            if (releasingColumns == 0)
                return 0.0;

            double columnFactor = 1.0 / (-25.0 / 66.0 * releasingColumns - 5.0 / 11.0) + 2.2;

            return release_long_note_weight * columnFactor * longNoteGate * nextNoteNerf(current);
        }

        /// <summary>
        /// If the next note starts too close to this release, the release difficulty should be reduced, with a
        /// rebound once the next note is far enough away that the motion is no longer being overlapped.
        /// </summary>
        private static double nextNoteNerf(ManiaDifficultyHitObject current)
        {
            const double min_ms = 80.0;
            const double max_ms = 220.0;

            ManiaDifficultyHitObject? nextInColumn = current.NextInColumn(0);

            if (nextInColumn == null)
                return 1.0;

            double gap = nextInColumn.StartTime - current.EndTime;

            if (gap <= min_ms)
                return 0.0;

            if (gap >= max_ms)
                return 1.0;

            return DiffUtils.Smoothstep(gap, min_ms, max_ms);
        }
    }
}