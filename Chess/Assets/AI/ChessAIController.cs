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
                    return @"# ""First Move""
# Picks the first legal move
turn = is_player_turn()
if turn == 1:
    n = get_legal_move_count()
    if n > 0:
        fc = get_move_from_col(0)
        fr = get_move_from_row(0)
        tc = get_move_to_col(0)
        tr = get_move_to_row(0)
        move(fc, fr, tc, tr)";

                case AIDifficulty.Medium:
                    return @"# ""Capture Seeker""
# Prefers captures, then first move
turn = is_player_turn()
if turn == 1:
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
# Captures + center + development
turn = is_player_turn()
if turn == 1:
    n = get_legal_move_count()
    best = 0
    bestScore = -999
    i = 0
    while i < n:
        score = 0
        f = get_move_flags(i)
        if f >= 1:
            score = score + 100
        tc = get_move_to_col(i)
        tr = get_move_to_row(i)
        cd = tc - 3
        if cd < 0:
            cd = 0 - cd
        rd = tr - 3
        if rd < 0:
            rd = 0 - rd
        score = score + 8 - cd - rd
        p = get_move_promo_type(i)
        if p > 0:
            score = score + 200
        if score > bestScore:
            bestScore = score
            best = i
        i = i + 1
    if n > 0:
        fc = get_move_from_col(best)
        fr = get_move_from_row(best)
        tc = get_move_to_col(best)
        tr = get_move_to_row(best)
        move(fc, fr, tc, tr)";

                case AIDifficulty.Expert:
                    return @"# ""Strategist""
# Material + captures + center
turn = is_player_turn()
if turn == 1:
    n = get_legal_move_count()
    best = 0
    bestScore = -9999
    i = 0
    while i < n:
        score = 0
        f = get_move_flags(i)
        if f >= 1:
            score = score + 150
        tc = get_move_to_col(i)
        tr = get_move_to_row(i)
        cd = tc - 3
        if cd < 0:
            cd = 0 - cd
        rd = tr - 3
        if rd < 0:
            rd = 0 - rd
        score = score + (8 - cd - rd) * 3
        p = get_move_promo_type(i)
        if p > 0:
            score = score + 500
        ck = is_in_check()
        if ck == 1:
            score = score + 50
        fc = get_move_from_col(i)
        fr = get_move_from_row(i)
        pt = get_piece_type(fc, fr)
        if pt == 2:
            score = score + 20
        if pt == 3:
            score = score + 20
        if pt == 1:
            if tr > 4:
                score = score + tr * 5
        if score > bestScore:
            bestScore = score
            best = i
        i = i + 1
    if n > 0:
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
