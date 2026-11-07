// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public readonly struct JudgementProbs
    {
        public JudgementProbs(double pMax, double p300, double p200, double p100, double p50)
        {
            Score = ManiaPerformanceCalculator.MAX_JUDGEMENT_WEIGHT * pMax + 300 * p300 + 200 * p200 + 100 * p100 + 50 * p50;

            Variance = (ManiaPerformanceCalculator.MAX_JUDGEMENT_WEIGHT - Score) * (ManiaPerformanceCalculator.MAX_JUDGEMENT_WEIGHT - Score) * pMax +
                       (300 - Score) * (300 - Score) * p300 +
                       (200 - Score) * (200 - Score) * p200 +
                       (100 - Score) * (100 - Score) * p100 +
                       (50 - Score) * (50 - Score) * p50 +
                       (0 - Score) * (0 - Score) * (1 - pMax - p300 - p200 - p100 - p50);

            Gamma = Math.Pow(ManiaPerformanceCalculator.MAX_JUDGEMENT_WEIGHT - Score, 3) * pMax +
                    Math.Pow(300 - Score, 3) * p300 +
                    Math.Pow(200 - Score, 3) * p200 +
                    Math.Pow(100 - Score, 3) * p100 +
                    Math.Pow(50 - Score, 3) * p50 +
                    Math.Pow(0 - Score, 3) * (1 - pMax - p300 - p200 - p100 - p50);
        }

        public readonly double Score;
        public readonly double Variance;
        public readonly double Gamma;
    }
}
