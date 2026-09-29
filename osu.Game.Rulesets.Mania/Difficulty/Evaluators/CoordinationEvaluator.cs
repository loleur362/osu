﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
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

            return coordinationDifficulty * total_weight;
        }

        /// <summary>
        /// Calculates the difficulty of both column boundaries for this column, with a boundary being the hypothetical "middle" of two columns.
        /// </summary>
        private static double calculateBoundaryPressure(ManiaDifficultyHitObject current)
        {
            const double boundary_pressure_weight = 1.2;

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

            // Past this the neighbouring column has had time to be forgotten about, and stops sharing the hand.
            const double activity_window_ms = 450.0;

            int adjacentColumn = left ? column - 1 : column + 1;

            // The neighbour is already down as part of this same press, so there is no hand sharing a boundary
            // here to speak of. This has to be asked of the current row: the previous-note lookup below only ever
            // returns the row before, so a chord's own columns are never within tolerance of it and testing the
            // delta against CHORD_TOLERANCE_MS can never catch this case.
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
        /// Dampens the difficulty of a hit object based on the density of nearby notes.
        /// </summary>
        // # Note: This targets rolls and other manipable high density patterns in higher key modes such as 7k where the boundary pressure would accumulate
        // # because I couldnt manage to catch them in manipdetection for some reason.
        // # In short, BoundaryPressure would accumulate a lot and inflate difficulty while manip detection wont nerf it
        // # because in 7k+ its usually accompagnied with other pattern and the easy roll slips through
        private static double densityDampenFor(ManiaDifficultyHitObject current, int totalColumns)
        {
            const double density_window_ms = 180.0;
            const double note_cap = 3.0; // only starts with 3 notes rolls or more
            const double density_dampen_end = 8.0;
            const double density_dampen_max = 0.91;

            int liveNeighbours = 0;

            // Ignore chords
            if (current.Row.Size >= Math.Min(3, Math.Floor(totalColumns / 3.0) + 1)) return 1.0;

            for (int otherColumn = 0; otherColumn < totalColumns; otherColumn++)
            {
                if (otherColumn == current.Column)
                    continue;

                double otherStart = current.LastStartTimeInColumn(otherColumn);

                if (double.IsNegativeInfinity(otherStart))
                    continue;

                double otherDelta = current.StartTime - otherStart;

                if (otherDelta < ChordUtils.CHORD_TOLERANCE_MS)
                    continue;

                if (otherDelta <= density_window_ms)
                    liveNeighbours++;
            }

            if (liveNeighbours < note_cap)
                return 1.0;

            double x = Math.Min(1.0, (liveNeighbours - note_cap) / (density_dampen_end - note_cap));
            // smooth dampening https://www.desmos.com/calculator/0jvvip7qeq
            return 1.0 - density_dampen_max * x * x * (3.0 - 2.0 * x);
        }

        /// <summary>
        /// What the extra columns of a chord cost to place. Notes past the first come for free with the press
        /// itself, so this only pays for the shape being wider than one finger.
        /// </summary>
        private static double calculateChordDifficulty(ManiaDifficultyHitObject current, int depthInChord, double columnDelta)
        {
            const double load_per_extra_column = 2.8;
            const double shapeBonusWeight = 1.75;

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

            return difficulty / Math.Log(current.Row.Size);
        }

        /// <summary>
        /// How much the hand has to re-place itself because the shape around the repeat changed. A shape that shares
        /// no column with the row before it is a jump rather than a repeat, so it earns none of this, and neither
        /// does one that follows a single note: a lone note is not a shape to morph out of, and comparing against it
        /// reports the largest difference available, which hands out the whole bonus for the least effort.
        /// </summary>
        private static double calculateShapeBonus(ManiaDifficultyHitObject current, double columnDelta)
        {
            if (current.Row.Previous() is not { } previous
                || previous.Size < 2
                || !ColumnPatternUtils.SharesColumn(previous.Columns, current.Row.Columns))
                return 0.0;

            double shapeBonus = DiffUtils.Smoothstep(ColumnPatternUtils.ChordDifference(previous.Columns, current.Row.Columns), 0.3, 0.75);

            // Each column held over costs the hand less than a fresh one, and the more keys there are to spread over,
            // the less any single one of them means.
            int shared = ColumnPatternUtils.SharedColumnCount(previous.Columns, current.Row.Columns);

            if (shared > 0)
            {
                double keymode = Math.Min(current.Row.TotalColumns, 9);
                shapeBonus *= Math.Pow(0.57 + keymode * 0.03, shared);
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
