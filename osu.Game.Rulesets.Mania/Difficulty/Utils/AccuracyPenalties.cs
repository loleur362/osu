// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Utils;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public class AccuracyPenalties
    {
        public static readonly double[] PENALTY_VALUES = [1, 0.91, 0.82, 0.73, 0.64, 0.55, 0.46, 0.37, 0.28, 0.19, 0.10, 0.01];
        public readonly double[] Accuracies = new double[PENALTY_VALUES.Length];

        public (long, long, long) Pack()
        {
            long a2 = (ushort)(Accuracies[1] * ushort.MaxValue);
            long a3 = (ushort)(Accuracies[2] * ushort.MaxValue);
            long a4 = (ushort)(Accuracies[3] * ushort.MaxValue);
            long p1 = ((long)ushort.MaxValue << 48) | (a2 << 32) | (a3 << 16) | a4;

            long a5 = (ushort)(Accuracies[4] * ushort.MaxValue);
            long a6 = (ushort)(Accuracies[5] * ushort.MaxValue);
            long a7 = (ushort)(Accuracies[6] * ushort.MaxValue);
            long a8 = (ushort)(Accuracies[7] * ushort.MaxValue);
            long p2 = (a5 << 48) | (a6 << 32) | (a7 << 16) | a8;

            long a9 = (ushort)(Accuracies[8] * ushort.MaxValue);
            long a10 = (ushort)(Accuracies[9] * ushort.MaxValue);
            long a11 = (ushort)(Accuracies[10] * ushort.MaxValue);
            long a12 = (ushort)(Accuracies[11] * ushort.MaxValue);
            long p3 = (a9 << 48) | (a10 << 32) | (a11 << 16) | a12;

            return (p1, p2, p3);
        }

        public static AccuracyPenalties Unpack(long p1, long p2, long p3)
        {
            return new AccuracyPenalties
            {
                Accuracies =
                {
                    [0] = (double)(ushort)(p1 >> 48) / ushort.MaxValue,
                    [1] = (double)(ushort)(p1 >> 32) / ushort.MaxValue,
                    [2] = (double)(ushort)(p1 >> 16) / ushort.MaxValue,
                    [3] = (double)(ushort)p1 / ushort.MaxValue,
                    [4] = (double)(ushort)(p2 >> 48) / ushort.MaxValue,
                    [5] = (double)(ushort)(p2 >> 32) / ushort.MaxValue,
                    [6] = (double)(ushort)(p2 >> 16) / ushort.MaxValue,
                    [7] = (double)(ushort)p2 / ushort.MaxValue,
                    [8] = (double)(ushort)(p3 >> 48) / ushort.MaxValue,
                    [9] = (double)(ushort)(p3 >> 32) / ushort.MaxValue,
                    [10] = (double)(ushort)(p3 >> 16) / ushort.MaxValue,
                    [11] = (double)(ushort)p3 / ushort.MaxValue,
                }
            };
        }

        public static double GetMultiplierAt(AccuracyPenalties penalties, double accuracy)
        {
            if (accuracy >= 1) return 1;
            if (accuracy <= penalties.Accuracies.Last()) return 0;

            int idx = 1;

            while (penalties.Accuracies[idx] > accuracy)
                idx++;

            double lo = PENALTY_VALUES[idx - 1];
            double hi = PENALTY_VALUES[idx];

            double amount = DiffUtils.ReverseLerp(accuracy, penalties.Accuracies[idx - 1], penalties.Accuracies[idx]);

            return Interpolation.Lerp(lo, hi, amount);
        }
    }
}
