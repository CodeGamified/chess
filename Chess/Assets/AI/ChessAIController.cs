// Copyright CodeGamified 2025-2026
// MIT License — Chess
using UnityEngine;
using Chess.Game;
using Chess.Scripting;

namespace Chess.AI
{
    /// <summary>
    /// AI controller for Chess — runs the SAME bytecode engine as the player.
    /// Each difficulty tier is a Python script compiled + executed by ChessProgram.
    /// Changing difficulty reloads a different script. No special C# logic.
    /// </summary>
    public class ChessAIController : MonoBehaviour
    {
        private ChessMatchManager _match;
        private AIDifficulty _difficulty;
        private ChessProgram _program;

        public AIDifficulty Difficulty => _difficulty;
        public ChessProgram Program => _program;

        public void Initialize(ChessMatchManager match, AIDifficulty difficulty)
        {
            _match = match;
            _program = gameObject.AddComponent<ChessProgram>();
            SetDifficulty(difficulty);
        }

        public void SetDifficulty(AIDifficulty difficulty)
        {
            _difficulty = difficulty;
            string code = GetSampleCode(difficulty);
            _program.Initialize(_match, code, $"AI_{difficulty}", true);
            Debug.Log($"[ChessAI] Difficulty → {difficulty} (running bytecode)");
        }

        // =================================================================
        // SAMPLE CODE — the actual AI logic, written in the same Python
        // subset the player uses. What you see IS what runs.
        // =================================================================

        public static string GetSampleCode(AIDifficulty difficulty)
        {
            switch (difficulty)
            {
                case AIDifficulty.Easy:
                    return @"# ""Random Mover""
# Picks a random legal move
turn:
    n = get_legal_move_count()
    if n > 0:
        t = get_total_moves()
        pick = t * 7 + 3
        pick = pick % n
        if pick < 0:
            pick = 0 - pick
        fc = get_move_from_col(pick)
        fr = get_move_from_row(pick)
        tc = get_move_to_col(pick)
        tr = get_move_to_row(pick)
        move(fc, fr, tc, tr)";

                case AIDifficulty.Medium:
                    return @"# ""Capture Seeker""
# Prefers captures, then first move
turn:
    n = get_legal_move_count()
    best = 0
    i = 0
    while i < n:
        f = get_move_flags(i)
        if f >= 1:
            best = i
            i = n
        i = i + 1
    if n > 0:
        fc = get_move_from_col(best)
        fr = get_move_from_row(best)
        tc = get_move_to_col(best)
        tr = get_move_to_row(best)
        move(fc, fr, tc, tr)";

                case AIDifficulty.Hard:
                    return @"# ""Tactician""
# Engine search at depth 3
turn:
    n = get_legal_move_count()
    if n > 0:
        best = engine_search(3)
        fc = get_move_from_col(best)
        fr = get_move_from_row(best)
        tc = get_move_to_col(best)
        tr = get_move_to_row(best)
        move(fc, fr, tc, tr)";

                case AIDifficulty.Expert:
                    return @"# ""Engine Expert""
# Deep alpha-beta search at depth 10
turn:
    n = get_legal_move_count()
    if n > 0:
        best = engine_search(10)
        fc = get_move_from_col(best)
        fr = get_move_from_row(best)
        tc = get_move_to_col(best)
        tr = get_move_to_row(best)
        move(fc, fr, tc, tr)";

                default:
                    return "# Unknown difficulty";
            }
        }
    }
}
