// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Processing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Technical : ManiaSkill
    {
        public const double ACCURACY_SCALE_FACTOR = 1.3;

        private readonly IDifficultyProcessor technicalProcessor;

        public Technical(Mod[] mods, IDifficultyProcessor technicalProcessor, double overallDifficulty)
            : base(mods, overallDifficulty, ACCURACY_SCALE_FACTOR, ACCURACY_SCALE_FACTOR)
        {
            this.technicalProcessor = technicalProcessor;
        }

        protected override NoteAccuracyCurve AccuracyCurveAt(DifficultyHitObject current)
        {
            technicalProcessor.ProcessStrainFor(current);

            return new NoteAccuracyCurve { Difficulty = technicalProcessor.CurrentStrain, ScaleFactor = ACCURACY_SCALE_FACTOR };
        }
    }
}
