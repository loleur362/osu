// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing.Patterning;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class CoordinationEvaluator
    {
        // The tap-skill quadrature weight (sqrt(3.3)) with the former 1.22x SS accuracy factor folded in,
        // now that accuracy multipliers are relative to SS.
        private const double coordination_multiplier = 2.22;

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
            double coordinationDifficulty = calculateBoundaryPressure(current);

            double columnDelta = current.ColumnDelta;
            int depthInChord = ChordUtils.DepthInChord(current);

            coordinationDifficulty += calculateChordDifficulty(current, depthInChord, columnDelta);
            coordinationDifficulty += calculateShieldDifficulty(current);
            coordinationDifficulty += calculateNoteWhileHolds(current);

            coordinationDifficulty *= minijackPressure(current);

            coordinationDifficulty *= current.ManipulationFactor * current.EnduranceFactor;

            return coordinationDifficulty * coordination_multiplier;
        }

        /// <summary>
        /// Two hands only have so many fingers, so past a point more columns being live at once stops adding
        /// difficulty as fast. Bends the top of the range over without ever capping it outright.
        /// </summary>
        /// 
        /// May not be needed anymore, higher SR is already kinda compressed.
        /// 
        /* private static double saturate(double strain)
        {
            const double threshold = 20.0;
            const double strength = 0.5;
            const double width = 1.5;

            // A softplus of the excess above the threshold.
            // See https://www.desmos.com/calculator/jgnbehwngr
            double z = (strain - threshold) / width;
            double softExcess = width * (Math.Max(z, 0.0) + Math.Log(1.0 + Math.Exp(-Math.Abs(z))));

            return strain - strength * softExcess;
        } */

        /// <summary>
        /// Calculates the difficulty of both column boundaries for this column, with a boundary being the hypothetical "middle" of two columns.
        /// </summary>
        private static double calculateBoundaryPressure(ManiaDifficultyHitObject current)
        {
            const double boundary_pressure_weight = 1.07;

            int column = current.Column;
            int totalColumns = current.Row.TotalColumns;
            double total = 0.0;

            if (column > 0)
                total += columnBoundaryPressure(current, column, left: true, totalColumns);

            if (column < totalColumns - 1)
                total += columnBoundaryPressure(current, column, left: false, totalColumns);

            return total * TrillUtils.TrillFactor(current) * boundary_pressure_weight;
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

            // capping intensity to nerf graces
            double intensity = Math.Min(scale_ms / (adjacentDelta + min_delta_ms), 20.0);
            double coefficient = CrossColumnUtils.ColumnBoundaryMultiplier(boundaryIndex, totalColumns);
            bool otherActive = adjacentDelta <= activity_window_ms;

            return intensity * coefficient * (otherActive ? 1.0 : (1.0 - coefficient)) * previousGapMultiplier(current, adjacentDelta);
        }

        /// <summary>
        /// How far back this column itself was last pressed, compared to how far back the neighbour was.
        /// A neighbour that is fresher than this column means the finger landing here is the one that has
        /// been resting, so the placement costs the hand less.
        /// </summary>
        private static double previousGapMultiplier(ManiaDifficultyHitObject current, double adjacentDelta)
        {
            const double min_share = 0.2;

            // How long this column has been idle, in comparison to the adjacent one.
            double ratio = current.ColumnDelta / adjacentDelta;

            return 1.0 - DiffUtils.Smoothstep(ratio, 2.5, 5.0) * (1 - min_share);
        }

        /// <summary>
        /// What the extra columns of a chord cost to place. Notes past the first come for free with the press
        /// itself, so this only pays for the shape being wider than one finger.
        /// </summary>
        private static double calculateChordDifficulty(ManiaDifficultyHitObject current, int depthInChord, double columnDelta)
        {
            const double load_per_extra_column = 2.75;
            const double shapeBonusWeight = 3.0;

            if (depthInChord < 2)
                return 0.0;

            double chordjackValue = calculateChordjackValue(current);

            // A series of chords with equal density in each rows but few shared columns
            // require a LOT more control than uneven chordjacks, example (O = note):
            //   left is much harder than right for the same density
            //          OOXOXXO                  XOXOXXX
            //          XOOXOOX                  OOOOOXO
            //          OXOOXOX                  XXXXOXO
            //          OOXXOXO                  OOXOOOO
            double balanceRatio = current.Row.Previous() is { } previous
                ? (double)Math.Min(previous.Size, current.Row.Size) / Math.Max(previous.Size, current.Row.Size)
                : 1.0;

            double difficulty = load_per_extra_column * (depthInChord - 1) * balanceRatio * ChordUtils.ChordRepeatNerf(current, columnDelta) * ChordUtils.ChordSpeedFactor(columnDelta)
                                + shapeBonusWeight * calculateShapeBonus(current, columnDelta);

            return difficulty * chordjackMultiplier(chordjackValue);
        }

        /// <summary>
        /// How much the current chord repeats the one before it.
        /// 0 for a chord with nothing shared (or no previous row), 1 for the same chord played again.
        /// </summary>
        private static double calculateChordjackValue(ManiaDifficultyHitObject current)
        {
            if (current.Row.Previous() is not { } previous)
                return 0.0;

            int shared = ColumnPatternUtils.SharedColumnCount(previous.Columns, current.Row.Columns);
            double averageSize = (previous.Size + current.Row.Size) / 2.0;

            return averageSize > 0.0 ? shared / averageSize : 0.0;
        }

        /// <summary>
        /// What a chord's repeat value does to its coordination. A chord that barely touches the one before it
        /// is a new shape the hand has to place and is worth more.
        /// </summary>
        private static double chordjackMultiplier(double chordjackValue)
        {
            if (chordjackValue <= 0.0)
                return 1.0;

            const double centre = 0.5;
            const double buff = 0.48;
            const double nerf = 0.6;

            return chordjackValue < centre
                ? 1.0 + buff * (centre - chordjackValue) / centre
                : 1.0 - nerf * (chordjackValue - centre) / (1.0 - centre);
        }

        /// <summary>
        /// How much the hand has to re-place itself because the shape around the repeat changed. A shape that either repeats 
        /// the exact same shape or shares no column with the row earns nothing.
        /// This is similar to chordjackMultiplier except it purely adds as a bonus.
        /// </summary>
        private static double calculateShapeBonus(ManiaDifficultyHitObject current, double columnDelta)
        {
            if (current.Row.Previous() is not { } previous
                || previous.Size < 1
                || !ColumnPatternUtils.SharesColumn(previous.Columns, current.Row.Columns))
                return 0.0;

            double shapeBonus = DiffUtils.Smoothstep(ColumnPatternUtils.ChordDifference(previous.Columns, current.Row.Columns), 0.3, 0.75);

            // Each repeated column asks less movement from the fingers, so they get progressively reduced
            int shared = ColumnPatternUtils.SharedColumnCount(previous.Columns, current.Row.Columns);

            if (shared > 1)
            {
                double keymode = Math.Min(current.Row.TotalColumns, 9);
                shapeBonus *= Math.Pow(0.61 + keymode * 0.04, shared);
            }

            // Slow transitions give the hand time to re-place.
            return shapeBonus * (1.0 - DiffUtils.Smoothstep(columnDelta, 100.0, 200.0));
        }

        /// <summary>
        /// A long note with a jack previously is a shield. If the long note needs to be held for an extended period of time,
        /// the hand has to fight against bouncing the finger up from the jack.
        /// </summary>
        ///         Hard shield  -->  O O===========|
        /// 
        ///         Easy shields -->  O   O=======| 
        ///                           O O===|
        private static double calculateShieldDifficulty(ManiaDifficultyHitObject current)
        {
            const double shield_weight = 1.00;

            const double jack_scale_ms = 120.0;
            const double jack_intensity = 320000.0;

            const double min_shield_ms = 150;
            const double full_shield_ms = 600;

            // Only a note that is actually being held can be a shield.
            double holdLength = current.EndTime - current.StartTime;

            if (holdLength <= 0.0)
                return 0.0;

            if (current.PrevInColumn(0) is null)
                return 0.0;

            double intensity = Math.Min(jack_intensity / (current.ColumnDelta * (current.ColumnDelta + jack_scale_ms)), 20.0);

            return shield_weight * intensity * DiffUtils.ReverseLerp(holdLength, min_shield_ms, full_shield_ms);
        }

        /// <summary>
        /// Other held notes makes the movement of a hit harder, the hand is already comitted to a note
        /// and spends more energy managing other patterns.
        /// </summary>
        private static double calculateNoteWhileHolds(ManiaDifficultyHitObject current)
        {
            const double held_note_weight = 1.5;

            return held_note_weight * (current.HeldNoteMultiplier - 1.0);
        }

        /// <summary>
        /// What it costs to land a minijack mid-flow. Moving this to coordination, this feeds each map's
        /// dominant skill instead of only jack.
        /// </summary>
        /// <remark>
        /// note 1: It currently activates kinda randomly, needs to be improved.
        /// note 2: okay it really is a mess lol ill try to simplify it later, I genuinely forgot what some of that do.
        /// </remark>
        private static double minijackPressure(ManiaDifficultyHitObject hitObject)
        {
            const double minijack_pressure_weight = 2.5;

            // A minijack is worth less when there are other jacks.
            const double minijack_other_penalty = 0.9;
            const double minijack_mult_floor = 0.02;

            const double window_ms_minijack = 300.0;

            const double minijack_fast_ms = 40.0;
            const double minijack_slow_ms = 135.0;

            // How much faster than the previous part the column has to come back to count as a minijack.
            const double acceleration_min = 1.0;
            const double acceleration_max = 2.5;

            // Density gate: count only returns that are as fast as (or faster than) this one, so continuing an
            // already-fast jack earns less than landing a fresh spike.
            const double density_window_ms = 400.0;
            const double density_low = 2.0;
            const double density_high = 4.0;
            const double similar_gap_margin = 1.0;

            const double runup_short_ms = 200.0;
            const double runup_long_ms = 800.0;
            const double runup_floor = 1.0;
            const double runup_reward_weight = 0.5;

            // A minijack with little else going on around it is not worth much, so fade the bonus out as the
            // surrounding notes thin out.
            const double local_density_window_ms = 250.0;
            const double local_density_low = 1.5;
            const double local_density_high = 3.5;

            if (hitObject.PrevInColumn(0) is not { } previousNote)
                return 1.0;

            double jackDelta = hitObject.StartTime - previousNote.StartTime;

            double previousPartDelta = previousNote.PrevInColumn(0) is { } beforePrevious
                ? previousNote.StartTime - beforePrevious.StartTime
                : 0.0;

            double acceleration = previousPartDelta > 0.0 ? previousPartDelta / Math.Max(jackDelta, 1.0) : 1.0;
            double speedUp = DiffUtils.Smoothstep(acceleration, acceleration_min, acceleration_max);

            double runUpFactor = runup_floor + runup_reward_weight * DiffUtils.Smoothstep(previousPartDelta, runup_short_ms, runup_long_ms);

            int localNotes = 0;

            for (var row = hitObject.Row.Previous(); row != null && hitObject.StartTime - row.StartTime <= local_density_window_ms; row = row.Previous())
                localNotes += row.Size;

            double localDensity = DiffUtils.Smoothstep(localNotes, local_density_low, local_density_high);

            double minijackMult = DiffUtils.Smoothstep(jackDelta, minijack_slow_ms, minijack_fast_ms);
            
            if (minijackMult <= 0) return 1.0;

            foreach (int column in hitObject.Row.Columns)
            {
                if (column == hitObject.Column)
                    continue;

                if (hitObject.PrevInColumnBefore(column, hitObject.Index) is not { } neighbour
                    || hitObject.PrevInColumnBefore(column, neighbour.Index) is not { } before)
                    continue;

                double otherDelta = neighbour.StartTime - before.StartTime;

                if (otherDelta > window_ms_minijack)
                    continue;

                minijackMult -= DiffUtils.Smoothstep(otherDelta, minijack_fast_ms, minijack_slow_ms) * minijack_other_penalty;
            }

            int similarOrFaster = 0;

            for (var earlier = previousNote; earlier != null && hitObject.StartTime - earlier.StartTime <= density_window_ms; earlier = earlier.PrevInColumn(0))
            {
                if (earlier.PrevInColumn(0) is not { } before)
                    break;

                double gap = earlier.StartTime - before.StartTime;

                if (gap <= jackDelta * similar_gap_margin)
                    similarOrFaster++;
            }

            double densityGate = 1.0 - DiffUtils.Smoothstep(similarOrFaster, density_low, density_high);

            return 1.0 + minijack_pressure_weight * speedUp * runUpFactor * localDensity * densityGate * Math.Max(minijackMult, minijack_mult_floor);
        }
    }
}
