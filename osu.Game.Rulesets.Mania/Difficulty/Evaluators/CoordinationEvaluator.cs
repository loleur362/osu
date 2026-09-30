// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class CoordinationEvaluator
    {
        /// <summary>
        /// Evaluates the difficulty of placing the current note relative to everything the other fingers are
        /// doing, based on:
        /// <list type="bullet">
        /// <item><description>how recently each neighbouring column was pressed,</description></item>
        /// <item><description>how wide the chord it sits in is,</description></item>
        /// <item><description>and how many long notes are being held when it is hit.</description></item>
        /// </list>
        /// </summary>

        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject current)
        {
            // Total combines the tap skills in quadrature, so this evaluator carries the square root of its weight.
            const double total_weight = 1.81659; // sqrt(3.3)

            double coordinationDifficulty = calculateBoundaryPressure(current);

            double columnDelta = current.ColumnDelta;
            int depthInChord = ChordUtils.DepthInChord(current);

            coordinationDifficulty += calculateChordDifficulty(current, depthInChord, columnDelta);
            coordinationDifficulty += calculateHoldDifficulty(current);

            coordinationDifficulty *= current.ManipulationFactor * current.EnduranceFactor;

            return saturate(coordinationDifficulty * total_weight);
        }

        /// <summary>
        /// Two hands only have so many fingers, so past a point more columns being live at once stops adding
        /// difficulty as fast. Bends the top of the range over without ever capping it outright.
        /// </summary>
        private static double saturate(double strain)
        {
            const double threshold = 14.0;
            const double strength = 0.75;
            const double width = 1.5;

            // A softplus of the excess above the threshold.
            // See https://www.desmos.com/calculator/jgnbehwngr
            double z = (strain - threshold) / width;
            double softExcess = width * (Math.Max(z, 0.0) + Math.Log(1.0 + Math.Exp(-Math.Abs(z))));

            return strain - strength * softExcess;
        }

        /// <summary>
        /// Calculates the difficulty of both column boundaries for this column, with a boundary being the hypothetical "middle" of two columns.
        /// </summary>
        private static double calculateBoundaryPressure(ManiaDifficultyHitObject current)
        {
            const double boundary_pressure_weight = 1.39;

            int column = current.Column;
            int totalColumns = current.Row.TotalColumns;
            double total = 0.0;

            if (column > 0)
                total += columnBoundaryPressure(current, column, left: true, totalColumns);

            if (column < totalColumns - 1)
                total += columnBoundaryPressure(current, column, left: false, totalColumns);

            return total * TrillUtils.TrillFactor(current) * boundary_pressure_weight * densityDampenFor(current, totalColumns);
        }

        /// <summary>
        /// How much pressure the column on one side of <paramref name="current"/> is putting on the hand, from how
        /// recently it was last pressed.
        /// </summary>
        private static double columnBoundaryPressure(ManiaDifficultyHitObject current, int column, bool left, int totalColumns)
        {
            const double scale_ms = 1300.0;
            const double min_delta_ms = 35.0;

            // Past this the neighbouring column has had time to be forgotten about.
            const double activity_window_ms = 450.0;

            int adjacentColumn = left ? column - 1 : column + 1;
            if (Array.IndexOf(current.Row.Columns, adjacentColumn) >= 0)
                return 0.0;

            double adjacentStartTime = current.LastStartTimeInColumn(adjacentColumn);
            if (double.IsNegativeInfinity(adjacentStartTime))
                return 0.0;

            double adjacentDelta = current.StartTime - adjacentStartTime;
            if (adjacentDelta < ChordUtils.CHORD_TOLERANCE_MS)
                return 0.0;

            // Boundaries sit between columns, so the left side boundary shares this column's index.
            int boundaryIndex = left ? column : column + 1;

            // capping intensity, graces are not nerfed enough
            double intensity = scale_ms / (adjacentDelta + min_delta_ms);
            double coefficient = CrossColumnUtils.ColumnBoundaryMultiplier(boundaryIndex, totalColumns);
            bool otherActive = adjacentDelta <= activity_window_ms;

            return intensity * coefficient * (otherActive ? 1.0 : (1.0 - coefficient));
        }


        /// <summary>
        /// Dampens the difficulty of a hit object based on streaks of notes in a column.
        /// </summary>
        // # Note: This targets rolls and other manipable high density patterns in higher key modes such as 7k where the boundary pressure would accumulate
        // # because I couldnt manage to catch them in manipdetection for some reason since in 7k+ its usually accompagnied with other pattern and the easy roll slips through
        private static double densityDampenFor(ManiaDifficultyHitObject current, int totalColumns)
        {
            const double tightest_gap_ms = 10.0;
            const double max_gap_ms = 70.0;

            const double tightest_nerf = 0.9;
            const double full_nerf = 1.0;

            // A long run stacks enough of these to take almost everything, but it still has to be worth something.
            const double min_dampen = 0.4;

            // A note with two neighbours can't be manipulated as easily, such as in brackets.
            const double opposite_margin_ms = 20.0;

            // Streaks of notes with holes are still accounted for but their nerf will weight for less.
            const double after_gap = 0.75;

            double dampen = 1.0;

            foreach (int direction in stackalloc[] { -1, 1 })
            {
                double runDampen = 1.0;
                double firstDiscount = 1.0;
                int neighbours = 0;
                bool weakened = false;
                int column = current.Column;

                while (true)
                {
                    int step = column + direction - current.Column;
                    column += direction;

                    if (column < 0 || column >= totalColumns)
                        break;

                    // Always the latest note in that column before this one.
                    ManiaDifficultyHitObject? neighbour = current.PrevInColumnBefore(column, current.Index);

                    // An empty column isnt always the end of the run, so the walk carries on past it.
                    if (neighbour is null)
                    {
                        weakened = true;
                        continue;
                    }

                    double gap = current.StartTime - neighbour.StartTime;

                    int oppositeColumn = current.Column - step;

                    if (oppositeColumn >= 0 && oppositeColumn < totalColumns
                        && current.PrevInColumnBefore(oppositeColumn, current.Index) is { } opposite
                        && current.StartTime - opposite.StartTime <= gap + opposite_margin_ms)
                        continue;

                    double discount = weakened ? 1.0 - (1.0 - discountFor(gap)) * after_gap : discountFor(gap);

                    // The nerf starts from the second neighbour.
                    if (neighbours++ == 0)
                        firstDiscount = discount;
                    else
                        runDampen *= discount;

                    weakened = false;
                }

                if (neighbours > 1)
                    runDampen *= firstDiscount;

                dampen = Math.Min(dampen, runDampen);
            }

            return Math.Max(dampen, min_dampen);

            double discountFor(double gap)
            {
                if (gap >= max_gap_ms)
                    return 1.0;

                return DiffUtils.ReverseLerp(gap, tightest_gap_ms, max_gap_ms) * (full_nerf - tightest_nerf) + tightest_nerf;
            }
        }

        /// <summary>
        /// What the extra columns of a chord cost to place. Notes past the first come for free with the press
        /// itself, so this only pays for the shape being wider than one finger.
        /// </summary>
        private static double calculateChordDifficulty(ManiaDifficultyHitObject current, int depthInChord, double columnDelta)
        {
            const double load_per_extra_column = 1.75;
            const double shapeBonusWeight = 1.5;

            if (depthInChord < 2)
                return 0.0;

            // Chordjacks are already paid for by Jack, so the dampening here only targets sustained chord spam.
            bool isChordjack = columnDelta <= JackEvaluator.JACK_WINDOW_MS;

            double difficulty = load_per_extra_column * (depthInChord - 1) * ChordUtils.ChordRepeatNerf(current, columnDelta)
                                * (isChordjack ? ChordUtils.CHORDJACK_NERF : 1.0) * ChordUtils.ChordSpeedFactor(columnDelta)
                                + shapeBonusWeight * calculateShapeBonus(current, columnDelta);

            // The same shape is a movement the hand is already doing rather than new fingers to
            // place, so it of course requires less coordination overall.
            if (current.Row.Previous() is { } previousShape && ColumnPatternUtils.SameColumns(previousShape.Columns, current.Row.Columns))
                difficulty *= 0.5;

            return difficulty;
        }

        /// <summary>
        /// How much the hand has to re-place itself because the shape around the repeat changed. A shape that either repeats or shares
        /// no column with the row earns nothing.
        /// </summary>
        private static double calculateShapeBonus(ManiaDifficultyHitObject current, double columnDelta)
        {
            if (current.Row.Previous() is not { } previous
                || previous.Size < 2
                || !ColumnPatternUtils.SharesColumn(previous.Columns, current.Row.Columns))
                return 0.0;

            double shapeBonus = DiffUtils.Smoothstep(ColumnPatternUtils.ChordDifference(previous.Columns, current.Row.Columns), 0.3, 0.75);

            // Each repeated column asks less movement from the fingers, so they get progressively reduced
            int shared = ColumnPatternUtils.SharedColumnCount(previous.Columns, current.Row.Columns);

            if (shared > 0)
            {
                double keymode = Math.Min(current.Row.TotalColumns, 9);
                shapeBonus *= Math.Pow(0.6 + keymode * 0.04, shared);
            }

            // Slow transitions give the hand time to re-place.
            return shapeBonus * (1.0 - DiffUtils.Smoothstep(columnDelta, 100.0, 200.0));
        }

        /// <summary>
        /// Long notes held in other columns take fingers out of play, and the less time there is between presses
        /// the more that costs.
        /// </summary>
        private static double calculateHoldDifficulty(ManiaDifficultyHitObject current)
        {
            const double held_long_note_weight = 0.25;
            const double held_speed_factor_offset = 0.08;
            const double hold_start_cap_end_ms = 35.0;

            // High difficulty cap
            const double soft_ceiling_midpoint = 2.719;

            int heldColumns = current.ConcurrentlyHeldColumns(ChordUtils.CHORD_TOLERANCE_MS);
            if (heldColumns == 0)
                return 0.0;

            double heldSpeedFactor = current.DeltaTime >= ChordUtils.CHORD_TOLERANCE_MS ? 1.0 / (current.DeltaTime / 1000.0 + held_speed_factor_offset) : 1.0;
            double columnFactor = 1.0 / (-25.0 / 66.0 * heldColumns - 5.0 / 11.0) + 2.2;
            double holdDifficulty = columnFactor * heldSpeedFactor; // https://www.desmos.com/calculator/aoqjrgqqht
            double difficultyCap = soft_ceiling_midpoint / (soft_ceiling_midpoint + holdDifficulty);
            // Grace notes are basically chords, they don't have a hold difficulty.
            double holdStartCap = DiffUtils.Smoothstep(current.DeltaTime, ChordUtils.CHORD_TOLERANCE_MS, hold_start_cap_end_ms);

            return holdStartCap * held_long_note_weight * holdDifficulty * difficultyCap;
        }
    }
}
