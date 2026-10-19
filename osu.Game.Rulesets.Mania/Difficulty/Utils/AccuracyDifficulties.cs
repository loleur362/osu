// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Utils;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    /// <summary>
    /// A note's difficulty, expressed as the skill needed to hit it at each of
    /// <see cref="AccuracyValueMultipliers.ACCURACY_VALUES"/>.
    /// </summary>
    public readonly struct AccuracyDifficulties
    {
        public double BaseDifficulty { get; }

        public double[] Multipliers { get; }

        public AccuracyDifficulties(double baseDifficulty, AccuracyValueMultipliers multipliers)
            : this(baseDifficulty, multipliers.AccuracyMultipliers)
        {
        }

        private AccuracyDifficulties(double baseDifficulty, double[] multipliers)
        {
            BaseDifficulty = baseDifficulty;
            Multipliers = multipliers;
        }

        /// <summary>
        /// The absolute skill this note asks for at a given accuracy index.
        /// </summary>
        private double getDifficulty(int index) => index == 0 ? BaseDifficulty : BaseDifficulty * Multipliers[index];

        public static AccuracyDifficulties Pow(AccuracyDifficulties difficultiesBase, int exponent)
        {
            double newBaseDifficulty = DiffUtils.Pow(difficultiesBase.BaseDifficulty, exponent);

            double[] newMultipliers = new double[difficultiesBase.Multipliers.Length];

            for (int i = 0; i < difficultiesBase.Multipliers.Length; i++)
                newMultipliers[i] = DiffUtils.Pow(difficultiesBase.Multipliers[i], exponent);

            return new AccuracyDifficulties(newBaseDifficulty, newMultipliers);
        }

        public static AccuracyDifficulties Pow(AccuracyDifficulties difficultiesBase, double exponent)
        {
            double newBaseDifficulty = Math.Pow(difficultiesBase.BaseDifficulty, exponent);

            double[] newMultipliers = new double[difficultiesBase.Multipliers.Length];

            for (int i = 0; i < difficultiesBase.Multipliers.Length; i++)
                newMultipliers[i] = Math.Pow(difficultiesBase.Multipliers[i], exponent);

            return new AccuracyDifficulties(newBaseDifficulty, newMultipliers);
        }

        public static AccuracyDifficulties operator +(AccuracyDifficulties left, AccuracyDifficulties right)
        {
            double newBaseDifficulty = left.BaseDifficulty + right.BaseDifficulty;

            double[] newMultipliers = new double[left.Multipliers.Length];

            for (int i = 0; i < left.Multipliers.Length - 1; i++)
                newMultipliers[i] = newBaseDifficulty > 0 ? (left.getDifficulty(i) + right.getDifficulty(i)) / newBaseDifficulty : 1;

            return new AccuracyDifficulties(newBaseDifficulty, newMultipliers);
        }

        public static AccuracyDifficulties operator *(AccuracyDifficulties left, double right)
        {
            double newBaseDifficulty = left.BaseDifficulty * right;

            return new AccuracyDifficulties(newBaseDifficulty, left.Multipliers);
        }

        /// <summary>
        /// The accuracy a player of the given skill is expected to hit this note at.
        /// </summary>
        public double AccuracyAt(double skill)
        {
            if (skill >= getDifficulty(0))
                return AccuracyValueMultipliers.ACCURACY_VALUES[0];
            if (skill <= 0)
                return AccuracyValueMultipliers.ACCURACY_VALUES[^1];

            for (int i = 1; i < Multipliers.Length; i++)
            {
                if (getDifficulty(i) > skill)
                    continue;

                double upperSkillBound = getDifficulty(i - 1);
                double lowerSkillBound = getDifficulty(i);

                double upperAccuracyBound = AccuracyValueMultipliers.ACCURACY_VALUES[i - 1];
                double lowerAccuracyBound = AccuracyValueMultipliers.ACCURACY_VALUES[i];

                return Interpolation.Lerp(lowerAccuracyBound, upperAccuracyBound, (skill - lowerSkillBound) / (upperSkillBound - lowerSkillBound));
            }

            return 0;
        }
    }
}
