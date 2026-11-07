// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Processing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Jack : ManiaSkill
    {
        public const double ACCURACY_SCALE_FACTOR = 3.3;

        private readonly IDifficultyProcessor jackProcessor;

        public Jack(Mod[] mods, IDifficultyProcessor jackProcessor, double overallDifficulty)
            : base(mods, overallDifficulty, ACCURACY_SCALE_FACTOR, ACCURACY_SCALE_FACTOR)
        {
            this.jackProcessor = jackProcessor;
        }

        protected override NoteAccuracyCurve AccuracyCurveAt(DifficultyHitObject current)
        {
            jackProcessor.ProcessStrainFor(current);

            return new NoteAccuracyCurve { Difficulty = jackProcessor.CurrentStrain, ScaleFactor = ACCURACY_SCALE_FACTOR };
        }
    }
}
