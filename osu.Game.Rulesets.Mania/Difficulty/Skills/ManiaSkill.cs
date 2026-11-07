// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public abstract class ManiaSkill : Skill
    {
        private const double star_rating_accuracy = 0.9575;
        private const double ss_damp = 300;

        private readonly CurveBinning binnedDifficulties;

        private double totalNoteWeight;

        private const double accuracy_prob = 1e-6;

        private readonly DifficultyHitWindows hitWindows;

        protected ManiaSkill(Mod[] mods, double overallDifficulty, double minCurveScale, double maxCurveScale)
            : base(mods)
        {
            hitWindows = new DifficultyHitWindows(overallDifficulty);
            binnedDifficulties = new CurveBinning(minCurveScale, maxCurveScale);
        }

        protected override double ProcessInternal(DifficultyHitObject current)
        {
            totalNoteWeight += getNoteWeight(current);

            NoteAccuracyCurve curve = AccuracyCurveAt(current);

            binnedDifficulties.AddToBins(curve);

            return curve.Difficulty;
        }

        protected abstract NoteAccuracyCurve AccuracyCurveAt(DifficultyHitObject current);

        private static double getNoteWeight(DifficultyHitObject current)
        {
            const double max_long_note_weight_duration_ms = 1000.0;
            const double long_note_weight_per_200_ms = 0.6;

            double noteWeight = 1;

            if (current.BaseObject is HoldNote holdNote)
            {
                double duration = Math.Min(holdNote.EndTime - holdNote.StartTime, max_long_note_weight_duration_ms);
                noteWeight += long_note_weight_per_200_ms * duration / 200.0;
            }

            return noteWeight;
        }

        public override double DifficultyValue() => DifficultyValueAtAccuracy(star_rating_accuracy);

        public double DifficultyValueAtAccuracy(double accuracy)
        {
            if (binnedDifficulties.Count == 0)
                return 0.0;

            double rawDifficulty = RootFinding.FindRootExpand(skill => AccuracyAtSkill(skill) - accuracy, 0, 100);

            const double note_count_offset = 34.64147;

            return rawDifficulty; // * (totalNoteWeight / (totalNoteWeight + note_count_offset));
        }

        public double AccuracyAtSkill(double skill)
        {
            if (skill <= 0)
                return 0.0;

            double sum = 0;
            double varSum = 0;
            double gammaSum = 0;

            foreach (var bin in binnedDifficulties.Bins)
            {
                if (bin.Count == 0)
                    continue;

                var noteProbs = getNoteProbabilities(bin.Curve, skill);

                sum += bin.Count * noteProbs.Score;
                varSum += bin.Count * noteProbs.Variance;
                gammaSum += bin.Count * noteProbs.Gamma;
            }

            sum += binnedDifficulties.ZeroDifficultyNoteCount * ManiaPerformanceCalculator.MAX_JUDGEMENT_WEIGHT;

            double mean = sum;
            double dev = Math.Sqrt(varSum) + ss_damp;
            double gamma = gammaSum;

            double maxScore = binnedDifficulties.Count * ManiaPerformanceCalculator.MAX_JUDGEMENT_WEIGHT;

            PoissonBinomial poiBin = new PoissonBinomial(mean, dev, gamma);

            double score = RootFinding.FindRootExpand(accuracy => (1 - poiBin.CDF(accuracy)) - accuracy_prob, 0, maxScore);

            return score / maxScore;
        }

        private JudgementProbs getNoteProbabilities(NoteAccuracyCurve curve, double skill)
        {
            if (skill >= curve.Difficulty) return new JudgementProbs(1, 0, 0, 0, 0);
            if (skill <= 0) return new JudgementProbs(0, 0, 0, 0, 0);

            double sqrt2 = Math.Sqrt(2);

            double dev = sqrt2 * (NoteAccuracyCurve.MAX_UNSTABLE_RATE / 10.0) * Math.Pow(1 - skill / curve.Difficulty, 1 / curve.ScaleFactor);

            double pMax = DiffUtils.Erf(hitWindows.HMax / (DiffUtils.SQRT2 * dev));
            double p300 = DiffUtils.Erf(hitWindows.H300 / (DiffUtils.SQRT2 * dev));
            double p200 = DiffUtils.Erf(hitWindows.H200 / (DiffUtils.SQRT2 * dev));
            double p100 = DiffUtils.Erf(hitWindows.H100 / (DiffUtils.SQRT2 * dev));
            double p50 = DiffUtils.Erf(hitWindows.H50 / (DiffUtils.SQRT2 * dev));

            return new JudgementProbs(pMax, p300 - pMax, p200 - p300, p100 - p200, p50 - p100);
        }

        /// <summary>
        /// The coefficients of a quartic fitted to the miss counts at each skill level.
        /// </summary>
        /// <returns>The coefficients for our penalty polynomial.</returns>
        public AccuracyPenalties GetAccuracyPenalties(double ssSkill)
        {
            AccuracyPenalties accuracyPenalties = new AccuracyPenalties();
            accuracyPenalties.Accuracies[0] = 1;

            // If there are no notes, we just return.
            if (ObjectDifficulties.Count == 0 || ObjectDifficulties.Max() == 0)
                return accuracyPenalties;

            for (int i = 1; i < accuracyPenalties.Accuracies.Length; i++)
            {
                double penaltyValue = AccuracyPenalties.PENALTY_VALUES[i];

                double penalizedSkill = ssSkill * penaltyValue;

                // We transform the value quite a bit to squash accuracy losses, which are minute at high skill and vast at low skill, into a straighter line for the polynomial.
                accuracyPenalties.Accuracies[i] = AccuracyAtSkill(penalizedSkill);
            }

            // List<(double Actual, double Polynomial)> benchmarks = accuracies.Keys.Zip(accuracies.Select(x => 1 - PolynomialPenaltyUtils.GetPenaltyAt(coefficients, x.Value))).ToList();
            // double maxError = benchmarks.Select(x => Math.Abs(x.Actual - x.Polynomial)).Max();
            //
            // Debug.Assert(maxError < 0.01, $"maxError: {maxError} was greater than 0.01");

            return accuracyPenalties;
        }

        public static double SquishForPolynomial(double scoreLoss) => scoreLoss; // Math.Log(700 * scoreLoss + 1);
    }
}
