// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Difficulty
{
    public class ManiaPerformanceCalculator : PerformanceCalculator
    {
        public const double MAX_JUDGEMENT_WEIGHT = 320;

        private const double base_coefficient = 4.243 * 0.8;
        private const double base_exponent = 2.470;

        private int countPerfect;
        private int countGreat;
        private int countGood;
        private int countOk;
        private int countMeh;
        private int countMiss;

        public ManiaPerformanceCalculator()
            : base(new ManiaRuleset())
        {
        }

        protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
        {
            var maniaAttributes = (ManiaDifficultyAttributes)attributes;

            countPerfect = score.Statistics.GetValueOrDefault(HitResult.Perfect);
            countGreat = score.Statistics.GetValueOrDefault(HitResult.Great);
            countGood = score.Statistics.GetValueOrDefault(HitResult.Good);
            countOk = score.Statistics.GetValueOrDefault(HitResult.Ok);
            countMeh = score.Statistics.GetValueOrDefault(HitResult.Meh);
            countMiss = score.Statistics.GetValueOrDefault(HitResult.Miss);

            double multiplier = 1.0;

            if (score.Mods.Any(m => m is ModNoFail))
                multiplier *= 0.75;
            if (score.Mods.Any(m => m is ModEasy))
                multiplier *= 0.5;

            double difficultyValue = computeDifficultyValue(maniaAttributes.StarRatingSS);
            double accuracyScale = computeAccuracyScale(calculateCustomAccuracy(), maniaAttributes);
            double totalValue = difficultyValue * accuracyScale * multiplier;

            double valueSS = difficultyValue * multiplier;
            double value99 = valueSS * computeAccuracyScale(0.99, maniaAttributes);
            double value98 = valueSS * computeAccuracyScale(0.98, maniaAttributes);
            double value97 = valueSS * computeAccuracyScale(0.97, maniaAttributes);
            double value96 = valueSS * computeAccuracyScale(0.96, maniaAttributes);
            double value95 = valueSS * computeAccuracyScale(0.95, maniaAttributes);

            double estimatedStarRatingPP = valueSS * computeAccuracyScale(0.9575, maniaAttributes);

            return new ManiaPerformanceAttributes
            {
                Difficulty = difficultyValue * accuracyScale,
                Total = totalValue,
                StarRatingPP = computeDifficultyValue(maniaAttributes.StarRating),
                StarRatingAccValue = estimatedStarRatingPP,
                ValueSS = valueSS,
                Value99 = value99,
                Value98 = value98,
                Value97 = value97,
                Value96 = value96,
                Value95 = value95,
                Scale99 = value99 / valueSS,
                Scale98 = value98 / value99,
                Scale97 = value97 / value98,
                Scale96 = value96 / value97,
                Scale95 = value95 / value96
            };
        }

        private double computeDifficultyValue(double starRating)
        {
            double baseValue = base_coefficient * DiffUtils.Pow(starRating, base_exponent);

            return baseValue;
        }

        private double computeAccuracyScale(double accuracy, ManiaDifficultyAttributes attributes)
        {
            var accPenalties = AccuracyPenalties.Unpack(attributes.P1, attributes.P2, attributes.P3);

            return Math.Pow(
                AccuracyPenalties.GetMultiplierAt(accPenalties, accuracy),
                base_exponent * ManiaDifficultyCalculator.STAR_RATING_EXPONENT
            );
        }

        /// <summary>
        /// Accuracy used to weight judgements independently from the score's actual accuracy.
        /// </summary>
        private double calculateCustomAccuracy()
        {
            if (totalHits == 0)
                return 0;

            return (countPerfect * MAX_JUDGEMENT_WEIGHT + countGreat * 300 + countGood * 200 + countOk * 100 + countMeh * 50) / (totalHits * MAX_JUDGEMENT_WEIGHT);
        }

        private double totalHits => countPerfect + countOk + countGreat + countGood + countMeh + countMiss;
    }
}
