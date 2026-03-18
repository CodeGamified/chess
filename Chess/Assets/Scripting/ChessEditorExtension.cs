// Copyright CodeGamified 2025-2026
// MIT License — Chess
using System.Collections.Generic;
using CodeGamified.Editor;

namespace Chess.Scripting
{
    /// <summary>
    /// Editor extension for Chess — provides game-specific options
    /// to CodeEditorWindow's option tree for tap-to-code editing.
    /// </summary>
    public class ChessEditorExtension : IEditorExtension
    {
        public List<EditorTypeInfo> GetAvailableTypes() => new();

        public List<EditorFuncInfo> GetAvailableFunctions() => new()
        {
            // Cell queries
            new EditorFuncInfo { Name = "get_cell",             Hint = "raw piece (col,row) → 0-12",      ArgCount = 2 },
            new EditorFuncInfo { Name = "get_piece_type",       Hint = "piece type (col,row) → 0-6",      ArgCount = 2 },
            new EditorFuncInfo { Name = "get_piece_color",      Hint = "piece color (col,row) → 0-2",     ArgCount = 2 },

            // Board state
            new EditorFuncInfo { Name = "get_total_moves",      Hint = "total half-moves played",          ArgCount = 0 },
            new EditorFuncInfo { Name = "get_white_pieces",     Hint = "white piece count",                ArgCount = 0 },
            new EditorFuncInfo { Name = "get_black_pieces",     Hint = "black piece count",                ArgCount = 0 },
            new EditorFuncInfo { Name = "get_white_material",   Hint = "white material value",             ArgCount = 0 },
            new EditorFuncInfo { Name = "get_black_material",   Hint = "black material value",             ArgCount = 0 },

            // Turn / check
            new EditorFuncInfo { Name = "is_player_turn",       Hint = "1 if your turn (white)",           ArgCount = 0 },
            new EditorFuncInfo { Name = "is_in_check",          Hint = "1 if in check",                    ArgCount = 0 },
            new EditorFuncInfo { Name = "get_game_state",       Hint = "0=play 1=mate 2=stale 3=draw",    ArgCount = 0 },

            // Castling
            new EditorFuncInfo { Name = "can_castle_k",         Hint = "1 if kingside castle legal",       ArgCount = 0 },
            new EditorFuncInfo { Name = "can_castle_q",         Hint = "1 if queenside castle legal",      ArgCount = 0 },

            // En passant
            new EditorFuncInfo { Name = "get_ep_col",           Hint = "en passant target col",            ArgCount = 0 },
            new EditorFuncInfo { Name = "get_ep_row",           Hint = "en passant target row",            ArgCount = 0 },

            // Legal moves
            new EditorFuncInfo { Name = "get_legal_move_count", Hint = "number of legal moves",            ArgCount = 0 },
            new EditorFuncInfo { Name = "get_move_from_col",    Hint = "from col of move i",               ArgCount = 1 },
            new EditorFuncInfo { Name = "get_move_from_row",    Hint = "from row of move i",               ArgCount = 1 },
            new EditorFuncInfo { Name = "get_move_to_col",      Hint = "to col of move i",                 ArgCount = 1 },
            new EditorFuncInfo { Name = "get_move_to_row",      Hint = "to row of move i",                 ArgCount = 1 },
            new EditorFuncInfo { Name = "get_move_flags",       Hint = "flags bitmask: 1=cap 2=cast 4=pro", ArgCount = 1 },
            new EditorFuncInfo { Name = "get_move_promo_type",  Hint = "promotion piece type (0 or 2-5)",  ArgCount = 1 },

            // Last move
            new EditorFuncInfo { Name = "get_last_from_col",    Hint = "last move from col",               ArgCount = 0 },
            new EditorFuncInfo { Name = "get_last_from_row",    Hint = "last move from row",               ArgCount = 0 },
            new EditorFuncInfo { Name = "get_last_to_col",      Hint = "last move to col",                 ArgCount = 0 },
            new EditorFuncInfo { Name = "get_last_to_row",      Hint = "last move to row",                 ArgCount = 0 },

            // Game result
            new EditorFuncInfo { Name = "get_winner",           Hint = "0=none 1=player 2=AI 3=draw",     ArgCount = 0 },
            new EditorFuncInfo { Name = "get_player_wins",      Hint = "player wins count",                ArgCount = 0 },
            new EditorFuncInfo { Name = "get_ai_wins",          Hint = "AI wins count",                    ArgCount = 0 },
            new EditorFuncInfo { Name = "get_input",            Hint = "keyboard input (-1 if none)",      ArgCount = 0 },

            // Commands
            new EditorFuncInfo { Name = "move",                 Hint = "move piece (fc,fr,tc,tr) → 1/0",  ArgCount = 4 },
            new EditorFuncInfo { Name = "set_promotion",        Hint = "set promo type (2=N 3=B 4=R 5=Q)", ArgCount = 1 },
        };

        public List<EditorMethodInfo> GetMethodsForType(string typeName) => new();

        public List<string> GetVariableNameSuggestions() => new()
        {
            "fc", "fr", "tc", "tr", "n", "i", "best",
            "turn", "flags", "piece", "color", "type",
            "check", "state", "mat_w", "mat_b", "score"
        };

        public List<string> GetStringLiteralSuggestions() => new();
    }
}
