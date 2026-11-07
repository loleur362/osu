// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Processing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Release : ManiaSkill
    {
        public const double ACCURACY_SCALE_FACTOR = 1.05;

        private readonly IDifficultyProcessor releaseProcessor;

        public Release(Mod[] mods, IDifficultyProcessor releaseProcessor, double overallDifficulty)
            : base(mods, overallDifficulty, ACCURACY_SCALE_FACTOR, ACCURACY_SCALE_FACTOR)
        {
            this.releaseProcessor = releaseProcessor;
        }

        protected override NoteAccuracyCurve AccuracyCurveAt(DifficultyHitObject current)
        {
            releaseProcessor.ProcessStrainFor(current);

            return new NoteAccuracyCurve { Difficulty = releaseProcessor.CurrentStrain, ScaleFactor = ACCURACY_SCALE_FACTOR };
        }
    }
}
