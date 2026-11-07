// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Evaluators;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Processing
{
    public class ReleaseProcessor : IDifficultyProcessor
    {
        private const double strain_decay_base = 0.89647;

        public double CurrentStrain { get; private set; }

        public void ProcessStrainFor(DifficultyHitObject current)
        {
            CurrentStrain *= DiffUtils.Pow(strain_decay_base, current.DeltaTime / 1000);

            CurrentStrain += ReleaseEvaluator.EvaluateDifficultyOf((ManiaDifficultyHitObject)current);
        }
    }
}
