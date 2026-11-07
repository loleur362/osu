// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Processing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Coordination : ManiaSkill
    {
        public const double ACCURACY_SCALE_FACTOR = 2.2;

        private readonly IDifficultyProcessor coordinationProcessor;

        public Coordination(Mod[] mods, IDifficultyProcessor coordinationProcessor, double overallDifficulty)
            : base(mods, overallDifficulty, ACCURACY_SCALE_FACTOR, ACCURACY_SCALE_FACTOR)
        {
            this.coordinationProcessor = coordinationProcessor;
        }

        protected override NoteAccuracyCurve AccuracyCurveAt(DifficultyHitObject current)
        {
            coordinationProcessor.ProcessStrainFor(current);

            return new NoteAccuracyCurve { Difficulty = coordinationProcessor.CurrentStrain, ScaleFactor = ACCURACY_SCALE_FACTOR };
        }
    }
}
