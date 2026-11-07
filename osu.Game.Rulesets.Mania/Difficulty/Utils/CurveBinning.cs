// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public class CurveBinning(double minCurveScale, double maxCurveScale)
    {
        // Difficulty binning is dynamic and grows as difficulty increases
        private const double initial_difficulty = 10.0;
        private const double difficulty_bin_ratio = 1.1;

        // Curve binning is fixed to 8 bins, and we only need 1 if the range is 0.
        private readonly int accuracyScaleBinCount = minCurveScale < maxCurveScale ? 4 : 1;

        // Initialize a zero bin as well, which is always set to length 1 as the curve scale doesn't matter when difficulty is 0.
        public List<Bin> Bins = new List<Bin>();
        public double ZeroDifficultyNoteCount;

        public double Count;

        public void AddToBins(NoteAccuracyCurve curve)
        {
            double difficultyIndex = curve.Difficulty >= initial_difficulty
                ? 1 + Math.Log(curve.Difficulty / initial_difficulty, difficulty_bin_ratio)
                : DiffUtils.ReverseLerp(curve.Difficulty, 0, initial_difficulty);

            int lowerDiffdex = (int)difficultyIndex;
            int higherDiffdex = lowerDiffdex + 1;

            while (Bins.Count < (higherDiffdex + 1) * accuracyScaleBinCount)
            {
                int group = Bins.Count / accuracyScaleBinCount;
                double diff = group == 0 ? 0 : initial_difficulty * Math.Pow(difficulty_bin_ratio, group - 1);

                for (int i = 0; i < accuracyScaleBinCount; i++)
                {
                    double scale = accuracyScaleBinCount > 1
                        ? minCurveScale + i / (accuracyScaleBinCount - 1.0) * (maxCurveScale - minCurveScale)
                        : minCurveScale;

                    Bins.Add(new Bin(new NoteAccuracyCurve { Difficulty = diff, ScaleFactor = scale }));
                }
            }

            // Position of the scale factor in bin units, in [0, count - 1].
            double curveBinIndex = accuracyScaleBinCount > 1
                ? DiffUtils.ReverseLerp(curve.ScaleFactor, minCurveScale, maxCurveScale) * (accuracyScaleBinCount - 1)
                : 0;

            int lowerBindex = accuracyScaleBinCount > 1 ? Math.Min((int)curveBinIndex, accuracyScaleBinCount - 2) : 0;
            int higherBindex = accuracyScaleBinCount > 1 ? lowerBindex + 1 : 0;

            double lowerBindexRatio = accuracyScaleBinCount > 1 ? 1 - (curveBinIndex - lowerBindex) : 1;
            double lowerDiffRatio = 1 - (difficultyIndex - lowerDiffdex);

            if (lowerDiffdex > 0)
            {
                Bins[lowerDiffdex * accuracyScaleBinCount + lowerBindex].Count += lowerDiffRatio * lowerBindexRatio;
                Bins[lowerDiffdex * accuracyScaleBinCount + higherBindex].Count += lowerDiffRatio * (1 - lowerBindexRatio);
            }
            else
            {
                ZeroDifficultyNoteCount += lowerDiffRatio;
            }

            Bins[higherDiffdex * accuracyScaleBinCount + lowerBindex].Count += (1 - lowerDiffRatio) * lowerBindexRatio;
            Bins[higherDiffdex * accuracyScaleBinCount + higherBindex].Count += (1 - lowerDiffRatio) * (1 - lowerBindexRatio);

            Count++;
        }

        public class Bin(NoteAccuracyCurve curve)
        {
            public NoteAccuracyCurve Curve = curve;

            public double Count;
        }
    }
}
