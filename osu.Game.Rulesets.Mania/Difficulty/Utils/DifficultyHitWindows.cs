// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.Difficulty.Utils
{
    public readonly struct DifficultyHitWindows(double overallDifficulty)
    {
        public double HMax => 22.4 - 0.6 * overallDifficulty;
        public double H300 => 64 - 3 * overallDifficulty;
        public double H200 => 97 - 3 * overallDifficulty;
        public double H100 => 127 - 3 * overallDifficulty;
        public double H50 => 151 - 3 * overallDifficulty;
    }
}
