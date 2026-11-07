// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Difficulty.Processing;
using osu.Game.Rulesets.Mania.Difficulty.Utils;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Difficulty.Skills
{
    public class Total : ManiaSkill
    {
        private readonly IReadonlyDifficultyProcessor coordinationProcessor;
        private readonly IReadonlyDifficultyProcessor jackProcessor;
        private readonly IReadonlyDifficultyProcessor releaseProcessor;
        private readonly IReadonlyDifficultyProcessor speedProcessor;
        private readonly IReadonlyDifficultyProcessor technicalProcessor;

        public Total(Mod[] mods,
                     IReadonlyDifficultyProcessor coordinationProcessor,
                     IReadonlyDifficultyProcessor jackProcessor,
                     IReadonlyDifficultyProcessor releaseProcessor,
                     IReadonlyDifficultyProcessor speedProcessor,
                     IReadonlyDifficultyProcessor technicalProcessor,
                     double overallDifficulty)
            : base(mods, overallDifficulty, Release.ACCURACY_SCALE_FACTOR, Jack.ACCURACY_SCALE_FACTOR)
        {
            this.coordinationProcessor = coordinationProcessor;
            this.jackProcessor = jackProcessor;
            this.releaseProcessor = releaseProcessor;
            this.speedProcessor = speedProcessor;
            this.technicalProcessor = technicalProcessor;
        }

        protected override NoteAccuracyCurve AccuracyCurveAt(DifficultyHitObject current)
        {
            const double combine_lambda = 2;

            NoteAccuracyCurve coordination = new NoteAccuracyCurve { Difficulty = coordinationProcessor.CurrentStrain, ScaleFactor = Coordination.ACCURACY_SCALE_FACTOR };
            NoteAccuracyCurve jack = new NoteAccuracyCurve { Difficulty = jackProcessor.CurrentStrain, ScaleFactor = Jack.ACCURACY_SCALE_FACTOR };
            NoteAccuracyCurve release = new NoteAccuracyCurve { Difficulty = releaseProcessor.CurrentStrain, ScaleFactor = Release.ACCURACY_SCALE_FACTOR };
            NoteAccuracyCurve speed = new NoteAccuracyCurve { Difficulty = speedProcessor.CurrentStrain, ScaleFactor = Speed.ACCURACY_SCALE_FACTOR };
            NoteAccuracyCurve technical = new NoteAccuracyCurve { Difficulty = technicalProcessor.CurrentStrain, ScaleFactor = Technical.ACCURACY_SCALE_FACTOR };

            NoteAccuracyCurve tap = NoteAccuracyCurve.PNormSum(combine_lambda, speed, jack, coordination, technical);

            // Release is added linearly (p = 1) on top of the tap difficulty.
            return NoteAccuracyCurve.PNormSum(1, tap, release);
        }
    }
}
