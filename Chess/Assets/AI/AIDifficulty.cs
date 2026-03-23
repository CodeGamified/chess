// Copyright CodeGamified 2025-2026
// MIT License — Chess

namespace Chess.AI
{
    /// <summary>
    /// AI difficulty tiers for Chess.
    /// Each tier maps to a different sample Python script.
    ///
    /// EASY:    Picks the first legal move.
    /// MEDIUM:  Prefers captures, then first move.
    /// HARD:    Engine alpha-beta search at depth 3.
    /// EXPERT:  Engine alpha-beta search at depth 10.
    /// </summary>
    public enum AIDifficulty
    {
        Easy,
        Medium,
        Hard,
        Expert
    }
}
