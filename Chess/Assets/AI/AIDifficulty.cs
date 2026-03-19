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
    /// HARD:    Capture priority + center control + development.
    /// EXPERT:  Material eval + captures + center + pawn structure.
    /// </summary>
    public enum AIDifficulty
    {
        Easy,
        Medium,
        Hard,
        Expert
    }
}
