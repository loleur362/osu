// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing
{
    /// <summary>
    /// Works out, once per note, how much the long notes being held at that moment are worth. Doing it here means
    /// every evaluator reads <see cref="ManiaDifficultyHitObject.HeldNoteMultiplier"/> instead of recalculating each time it.
    /// </summary>
    public static class HeldNoteMultiplier
    {
        private const double hold_weight = 0.78;

        private const double hold_ramp_ms = 600.0;
        private const double hold_end_ramp_ms = 110.0;
        private const double hold_edge_factor = 0.4;

        /// A lenght shroter than this costs much less.
        private const double short_hold_ms = 110.0;
        private const double short_hold_steepness = 0.07;

        // Long notes placed farther from current get a reduction. A held note far from the tap \
        // interfers way less than neighbouring holds.
        private const double hold_column_falloff = 0.12;

        public static void ProcessAndAssign(ManiaMapData mapData)
        {
            foreach (var row in mapData.Rows)
            {
                foreach (var note in row.Objects)
                    note.HeldNoteMultiplier = evaluate(note);
            }
        }

        /// <summary>
        /// A multiplier that ranges >1, it increases at each simultaneously held columns of current's timestamp.
        /// An expensive hold is one that has been sustained for a long time, and has yet to be released.
        /// A cheap hold is one that is either short, ending soon or has just begun.
        /// </summary>
        private static double evaluate(ManiaDifficultyHitObject current)
        {
            double pressure = 0.0;

            for (int column = 0; column < current.HeadOverlappedHolds.Length; column++)
            {
                var hold = current.HeadOverlappedHolds[column];

                if (hold is null)
                    continue;

                double distanceWeight = 1.0 / (1.0 + hold_column_falloff * Math.Abs(column - current.Column));
                double contribution = heldNoteFactor(hold.StartTime, hold.EndTime, current.StartTime) * distanceWeight;

                pressure += contribution;
            }

            // A hold in this very row has only just been pressed, so it counts as the smallest amount.
            if (current.BaseObject is HoldNote)
            {
                double contribution = heldNoteFactor(current.StartTime, current.EndTime, current.StartTime);

                pressure += contribution;
            }

            return 1.0 + hold_weight * pressure;
        }

        /// <summary>
        /// Computes the actual multiplier for one long note.
        /// Takes in account the distance from startTime, endTime and its total lenght.
        /// </summary>
        private static double heldNoteFactor(double startTime, double endTime, double time)
        {
            double heldFor = time - startTime;
            double stillToGo = endTime - time;

            if (heldFor < 0.0 || stillToGo < 0.0)
                return 0.0;

            double rampIn = DiffUtils.Smoothstep(heldFor, 0.0, hold_ramp_ms);
            double rampOut = DiffUtils.Smoothstep(stillToGo, 0.0, hold_end_ramp_ms);

            double shortHoldGate = DiffUtils.Logistic(endTime - startTime, short_hold_ms, short_hold_steepness);

            return (hold_edge_factor + (1.0 - hold_edge_factor) * rampIn * rampOut) * shortHoldGate;
        }
    }
}
