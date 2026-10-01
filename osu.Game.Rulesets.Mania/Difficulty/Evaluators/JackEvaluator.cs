// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators.Jack;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    public static class JackEvaluator
    {
        /// <summary>
        /// Column gaps longer than this are too far apart to be jacked at all.
        /// </summary>
        public const double JACK_WINDOW_MS = 350.0;

        /// <summary>
        /// Evaluates the difficulty of hitting the current note with the same finger that just played its column,
        /// based on:
        /// <list type="bullet">
        /// <item><description>how quickly the column comes back to itself,</description></item>
        /// <item><description>the chord the repeat happens inside,</description></item>
        /// <item><description>long notes held in other columns at the time,</description></item>
        /// <item><description>and how much of the repeat the map lets you hit some other way.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateDifficultyOf(ManiaDifficultyHitObject current)
        {
            const double tap_rate_offset_ms = 60;
            const double strain_exponent = 1.29407;
            const double jack_multiplier = 0.592;

            // Total combines the tap skills in quadrature, so this evaluator carries the square root of its weight.
            const double total_weight = 1.19496; // sqrt(1.42793)

            double columnDelta = current.ColumnDelta;

            //TODO: The whole chordjack evaluation IS being revamped.
            if (columnDelta > JACK_WINDOW_MS)
                return 0.0;

            int chordDepth = ChordUtils.DepthInChord(current);
            double tapRate = 1000.0 / (Math.Max(columnDelta, 1.0) + tap_rate_offset_ms);

            bool sharesColumn = current.Row.Previous() is { } previousRow
                                && ColumnPatternUtils.SharesColumn(previousRow.Columns, current.Row.Columns);

            // How quickly the column comes back to itself, scaled by the chord it repeats inside.
            double jackDifficulty = tapRate * calculateChordJackBonus(current, chordDepth, columnDelta) * calculateSpeedBonus(tapRate);

            jackDifficulty = DiffUtils.Pow(jackDifficulty, strain_exponent);

            jackDifficulty *= calculateChordDepthMultiplier(current, chordDepth, columnDelta, sharesColumn);
            jackDifficulty *= calculateConcurrentHoldBonus(current);

            // Repeats that ask for more than their rate suggests.
            jackDifficulty *= FullChordJackEvaluator.EvaluateMultiplierOf(current, columnDelta, jackDifficulty * jack_multiplier);
            jackDifficulty *= current.ManipulationFactor * current.EnduranceFactor * SpeedjackEvaluator.EvaluateMultiplierOf(current) * AnchorEvaluator.EvaluateMultiplierOf(current);

            // Repeats the map lets you hit with something other than a jack motion.
            jackDifficulty *= JackSpacingEvaluator.EvaluateMultiplierOf(current, chordDepth, columnDelta, tapRate);

            return jackDifficulty * jack_multiplier * total_weight;
        }

        /// <summary>
        /// How much the chord this note sits in adds to the repeat, tapering off as the chord repeats.
        /// </summary>
        private static double calculateChordJackBonus(ManiaDifficultyHitObject current, int chordDepth, double columnDelta)
        {
            return Math.Max(0.1,
                (1.0 + 0.17460 * ChordUtils.ChordSpeedFactor(columnDelta) * (chordDepth - 1))
                * ChordUtils.ChordRepeatNerf(current, columnDelta));
        }

        /// <summary>
        /// Fast repeats cost more than their rate alone suggests, since there is no time to reposition between them.
        /// </summary>
        private static double calculateSpeedBonus(double tapRate)
        {
            // Centred on 5 notes per second, which is roughly a 1/4 note at 150bpm.
            return 1.0 + 0.7 * DiffUtils.Logistic(tapRate, 5.0, 0.5);
        }

        /// <summary>
        /// What the width of the chord does to the repeat. Chord jacks are worth most around 160bpm and less to
        /// either side of it, and past ~190bpm a repeat on a wide chord is a roll or vibro that can be mashed, so
        /// it rolls back off. A repeat that fast on jumps and single notes has to be jacked, and keeps its value.
        /// </summary>
        private static double calculateChordDepthMultiplier(ManiaDifficultyHitObject current, int chordDepth, double columnDelta, bool sharesColumn)
        {
            const double middle_ms = 100.0;
            const double smoothness = 3.9;
            const double midpoint = 1.75;

            const double middle_value = 2.35344; // This has to match the middle_ms y axis of the function
            const double saturation_ceiling = 2.6;
            const double saturation_floor_ms = 10.0;
            const double slowest_mult = 0.3;
            const double same_shape_nerf = 0.6;

            if (chordDepth < 2)
                return TrillUtils.TrillFactor(current);

            // Past 100ms the curve is on its decaying branch, kept from falling below a slow repeat's floor. https://www.desmos.com/calculator/knpvo3f8xr
            double chordSpeedMultiplier = Math.Max(slowest_mult, midpoint * smoothness / (0.1 * columnDelta - 11.0 + smoothness));

            // Below 100ms a ceilling stops the jack from scaling too much
            double speedShare = DiffUtils.Smoothstep(columnDelta, saturation_floor_ms, middle_ms);

            if (speedShare < 1.0)
                chordSpeedMultiplier = middle_value + (saturation_ceiling - middle_value) * (1.0 - speedShare);

            chordSpeedMultiplier *= calculateHandRestMultiplier(current);

            // The same shape again is one press the hand is already doing rather than a new one to place.
            if (current.Row.Previous() is { } previousShape
                && ColumnPatternUtils.SameColumns(previousShape.Columns, current.Row.Columns))
                chordSpeedMultiplier *= same_shape_nerf;

            return ChordUtils.CHORDJACK_NERF * chordSpeedMultiplier;
        }

        /// <summary>
        /// An empty row in a hand (half of the playfield) gives it time to rest and eases fast patterns.
        /// </summary>
        private static double calculateHandRestMultiplier(ManiaDifficultyHitObject current)
        {
            const double empty_hand_nerf = 0.75;
            const double no_shared_column_nerf = 0.9;

            int totalColumns = current.Row.TotalColumns;
            int[] columns = current.Row.Columns;

            if (current.Row.Previous() is { } previous
                && !ColumnPatternUtils.SharesColumn(previous.Columns, columns))
                return no_shared_column_nerf;

            // Half the keymode per hand, and the odd column in the middle goes to the other hand.
            int handSplit = totalColumns / 2;

            bool leftPlayed = false;
            bool rightPlayed = false;

            foreach (int column in columns)
            {
                if (column < handSplit)
                    leftPlayed = true;
                else
                    rightPlayed = true;

                if (leftPlayed && rightPlayed)
                    return 1.0;
            }

            return empty_hand_nerf;
        }

        /// <summary>
        /// Long notes held in other columns while this note is hit make it harder to place.
        /// </summary>
        private static double calculateConcurrentHoldBonus(ManiaDifficultyHitObject current)
        {
            const double held_bonus_weight = 0.785;
            const double held_neighbour_weight = 0.95;
            int totalColumns = current.PreviousHitObjects.Length;

            if (totalColumns == 1)
                return 1.0;

            // A jack next to a note is harder to hit because the hand is already pinned down and can't bounce the finger back up
            int heldNeighbours = 0;

            for (int offset = -1; offset <= 1; offset += 2)
            {
                int adjacentColumn = current.Column + offset;

                if (adjacentColumn < 0 || adjacentColumn >= totalColumns)
                    continue;

                // A hold that started in this same chord is part of one press, not a finger already committed.
                if (Math.Abs(current.LastStartTimeInColumn(adjacentColumn) - current.StartTime) <= ChordUtils.CHORD_TOLERANCE_MS)
                    continue;

                if (current.LastEndTimeInColumn(adjacentColumn) > current.StartTime + ChordUtils.CHORD_TOLERANCE_MS)
                    heldNeighbours++;
            }

            return held_bonus_weight * (1 + heldNeighbours * held_neighbour_weight);
        }
    }
}
