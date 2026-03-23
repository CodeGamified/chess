// Copyright CodeGamified 2025-2026
// MIT License — Chess
using System.Collections.Generic;
using CodeGamified.Engine;
using CodeGamified.Engine.Compiler;

namespace Chess.Scripting
{
    /// <summary>
    /// Chess opcodes mapped to CUSTOM_0..CUSTOM_N.
    /// 30 opcodes: 27 queries + 3 commands.
    ///
    /// Piece encoding (raw byte):
    ///   0 = empty
    ///   1-6 = white (Pawn, Knight, Bishop, Rook, Queen, King)
    ///   7-12 = black (Pawn, Knight, Bishop, Rook, Queen, King)
    ///
    /// Piece type (for get_piece_type / set_promotion):
    ///   0=none, 1=pawn, 2=knight, 3=bishop, 4=rook, 5=queen, 6=king
    ///
    /// Color: 0=none, 1=white(player), 2=black(AI)
    /// </summary>

    /// <summary>
    /// FINAL Chess opcode layout — exactly 32 opcodes (CUSTOM_0..CUSTOM_31).
    /// </summary>
    public enum ChessOp
    {
        // ── Cell queries (R0=col, R1=row → R0) ──
        GET_CELL           = 0,    // raw piece (0-12)
        GET_PIECE_TYPE     = 1,    // piece type (0-6)
        GET_PIECE_COLOR    = 2,    // color (0-2)

        // ── Board state → R0 ──
        GET_TOTAL_MOVES    = 3,
        GET_WHITE_PIECES   = 4,
        GET_BLACK_PIECES   = 5,
        GET_WHITE_MATERIAL = 6,
        GET_BLACK_MATERIAL = 7,

        // ── Turn / check / end state → R0 ──
        IS_PLAYER_TURN     = 8,
        IS_IN_CHECK        = 9,
        GET_GAME_STATE     = 10,   // 0=playing, 1=checkmate, 2=stalemate, 3=draw

        // ── Castling → R0 ──
        CAN_CASTLE_K       = 11,   // can castle kingside now (fully legal)
        CAN_CASTLE_Q       = 12,   // can castle queenside now (fully legal)

        // ── En passant → R0 ──
        GET_EP_COL         = 13,
        GET_EP_ROW         = 14,

        // ── Legal move enumeration ──
        GET_LEGAL_MOVE_COUNT = 15,
        GET_MOVE_FROM_COL    = 16,  // R0=idx → from col
        GET_MOVE_FROM_ROW    = 17,
        GET_MOVE_TO_COL      = 18,
        GET_MOVE_TO_ROW      = 19,
        GET_MOVE_FLAGS       = 20,  // R0=idx → flags bitmask (1=capture,2=castle,4=promo,8=ep)
        GET_MOVE_PROMO_TYPE  = 21,  // R0=idx → promotion piece type (2-5, or 0)

        // ── Last move → R0 ──
        GET_LAST_FROM_COL  = 22,
        GET_LAST_FROM_ROW  = 23,
        GET_LAST_TO_COL    = 24,
        GET_LAST_TO_ROW    = 25,

        // ── Game result → R0 ──
        GET_WINNER         = 26,   // 0=none, 1=player, 2=AI, 3=draw
        GET_PLAYER_WINS    = 27,
        GET_AI_WINS        = 28,
        GET_INPUT          = 29,

        // ── Commands ──
        MOVE               = 30,   // move(R0=fc, R1=fr, R2=tc, R3=tr) → R0: 1/0
        SET_PROMOTION      = 31,   // set_promotion(R0=type: 2=N, 3=B, 4=R, 5=Q)

        // ── Engine AI ──
        ENGINE_SEARCH      = 32,   // R0=depth → R0=best move index, R1=eval (cp)
        ENGINE_EVAL        = 33,   // → R0=static eval (centipawns, side-to-move POV)
        ENGINE_EVAL_MOVE   = 34,   // R0=move index → R0=eval after move (cp)
        ENGINE_PIECE_VALUE = 35,   // R0=piece type (1-6) → R0=centipawn value
        ENGINE_SEARCH_SCORE = 36,  // → R0=eval from last engine_search
        ENGINE_SEARCH_DEPTH = 37,  // → R0=depth completed in last engine_search
    }

    /// <summary>
    /// Compiler extension for Chess — registers builtins.
    /// </summary>
    public class ChessCompilerExtension : ICompilerExtension
    {
        public void RegisterBuiltins(CompilerContext ctx) { }

        public bool TryCompileCall(string functionName, List<AstNodes.ExprNode> args,
                                   CompilerContext ctx, int sourceLine)
        {
            switch (functionName)
            {
                // ── Cell queries (two-arg: col, row) ──
                case "get_cell":
                    CompileTwoArgs(args, ctx);
                    Emit(ctx, ChessOp.GET_CELL, sourceLine, "get_cell(R0=col,R1=row) → R0");
                    return true;
                case "get_piece_type":
                    CompileTwoArgs(args, ctx);
                    Emit(ctx, ChessOp.GET_PIECE_TYPE, sourceLine, "get_piece_type(R0=col,R1=row) → R0");
                    return true;
                case "get_piece_color":
                    CompileTwoArgs(args, ctx);
                    Emit(ctx, ChessOp.GET_PIECE_COLOR, sourceLine, "get_piece_color(R0=col,R1=row) → R0");
                    return true;

                // ── Board state ──
                case "get_total_moves":
                    Emit(ctx, ChessOp.GET_TOTAL_MOVES, sourceLine, "get_total_moves → R0");
                    return true;
                case "get_white_pieces":
                    Emit(ctx, ChessOp.GET_WHITE_PIECES, sourceLine, "get_white_pieces → R0");
                    return true;
                case "get_black_pieces":
                    Emit(ctx, ChessOp.GET_BLACK_PIECES, sourceLine, "get_black_pieces → R0");
                    return true;
                case "get_white_material":
                    Emit(ctx, ChessOp.GET_WHITE_MATERIAL, sourceLine, "get_white_material → R0");
                    return true;
                case "get_black_material":
                    Emit(ctx, ChessOp.GET_BLACK_MATERIAL, sourceLine, "get_black_material → R0");
                    return true;

                // ── Turn / check ──
                case "is_player_turn":
                    Emit(ctx, ChessOp.IS_PLAYER_TURN, sourceLine, "is_player_turn → R0");
                    return true;
                case "is_in_check":
                    Emit(ctx, ChessOp.IS_IN_CHECK, sourceLine, "is_in_check → R0");
                    return true;
                case "get_game_state":
                    Emit(ctx, ChessOp.GET_GAME_STATE, sourceLine, "get_game_state → R0 (0=play,1=mate,2=stale,3=draw)");
                    return true;

                // ── Castling ──
                case "can_castle_k":
                    Emit(ctx, ChessOp.CAN_CASTLE_K, sourceLine, "can_castle_k → R0");
                    return true;
                case "can_castle_q":
                    Emit(ctx, ChessOp.CAN_CASTLE_Q, sourceLine, "can_castle_q → R0");
                    return true;

                // ── En passant ──
                case "get_ep_col":
                    Emit(ctx, ChessOp.GET_EP_COL, sourceLine, "get_ep_col → R0");
                    return true;
                case "get_ep_row":
                    Emit(ctx, ChessOp.GET_EP_ROW, sourceLine, "get_ep_row → R0");
                    return true;

                // ── Legal move enumeration ──
                case "get_legal_move_count":
                    Emit(ctx, ChessOp.GET_LEGAL_MOVE_COUNT, sourceLine, "get_legal_move_count → R0");
                    return true;
                case "get_move_from_col":
                    CompileOneArg(args, ctx);
                    Emit(ctx, ChessOp.GET_MOVE_FROM_COL, sourceLine, "get_move_from_col(R0=idx) → R0");
                    return true;
                case "get_move_from_row":
                    CompileOneArg(args, ctx);
                    Emit(ctx, ChessOp.GET_MOVE_FROM_ROW, sourceLine, "get_move_from_row(R0=idx) → R0");
                    return true;
                case "get_move_to_col":
                    CompileOneArg(args, ctx);
                    Emit(ctx, ChessOp.GET_MOVE_TO_COL, sourceLine, "get_move_to_col(R0=idx) → R0");
                    return true;
                case "get_move_to_row":
                    CompileOneArg(args, ctx);
                    Emit(ctx, ChessOp.GET_MOVE_TO_ROW, sourceLine, "get_move_to_row(R0=idx) → R0");
                    return true;
                case "get_move_flags":
                    CompileOneArg(args, ctx);
                    Emit(ctx, ChessOp.GET_MOVE_FLAGS, sourceLine, "get_move_flags(R0=idx) → R0 bitmask");
                    return true;
                case "get_move_promo_type":
                    CompileOneArg(args, ctx);
                    Emit(ctx, ChessOp.GET_MOVE_PROMO_TYPE, sourceLine, "get_move_promo_type(R0=idx) → R0");
                    return true;

                // ── Last move ──
                case "get_last_from_col":
                    Emit(ctx, ChessOp.GET_LAST_FROM_COL, sourceLine, "get_last_from_col → R0");
                    return true;
                case "get_last_from_row":
                    Emit(ctx, ChessOp.GET_LAST_FROM_ROW, sourceLine, "get_last_from_row → R0");
                    return true;
                case "get_last_to_col":
                    Emit(ctx, ChessOp.GET_LAST_TO_COL, sourceLine, "get_last_to_col → R0");
                    return true;
                case "get_last_to_row":
                    Emit(ctx, ChessOp.GET_LAST_TO_ROW, sourceLine, "get_last_to_row → R0");
                    return true;

                // ── Game result ──
                case "get_winner":
                    Emit(ctx, ChessOp.GET_WINNER, sourceLine, "get_winner → R0");
                    return true;
                case "get_player_wins":
                    Emit(ctx, ChessOp.GET_PLAYER_WINS, sourceLine, "get_player_wins → R0");
                    return true;
                case "get_ai_wins":
                    Emit(ctx, ChessOp.GET_AI_WINS, sourceLine, "get_ai_wins → R0");
                    return true;
                case "get_input":
                    Emit(ctx, ChessOp.GET_INPUT, sourceLine, "get_input → R0");
                    return true;

                // ── Commands ──
                case "move":
                    CompileFourArgs(args, ctx);
                    Emit(ctx, ChessOp.MOVE, sourceLine, "move(R0=fc,R1=fr,R2=tc,R3=tr) → R0");
                    return true;
                case "set_promotion":
                    CompileOneArg(args, ctx);
                    Emit(ctx, ChessOp.SET_PROMOTION, sourceLine, "set_promotion(R0=type) 2=N,3=B,4=R,5=Q");
                    return true;

                // ── Engine AI ──
                case "engine_search":
                    CompileOneArg(args, ctx);
                    Emit(ctx, ChessOp.ENGINE_SEARCH, sourceLine, "engine_search(R0=depth) → R0=move idx, R1=eval");
                    return true;
                case "engine_eval":
                    Emit(ctx, ChessOp.ENGINE_EVAL, sourceLine, "engine_eval → R0 (centipawns)");
                    return true;
                case "engine_eval_move":
                    CompileOneArg(args, ctx);
                    Emit(ctx, ChessOp.ENGINE_EVAL_MOVE, sourceLine, "engine_eval_move(R0=idx) → R0 (centipawns)");
                    return true;
                case "engine_piece_value":
                    CompileOneArg(args, ctx);
                    Emit(ctx, ChessOp.ENGINE_PIECE_VALUE, sourceLine, "engine_piece_value(R0=type) → R0 (centipawns)");
                    return true;
                case "engine_search_score":
                    Emit(ctx, ChessOp.ENGINE_SEARCH_SCORE, sourceLine, "engine_search_score → R0");
                    return true;
                case "engine_search_depth":
                    Emit(ctx, ChessOp.ENGINE_SEARCH_DEPTH, sourceLine, "engine_search_depth → R0");
                    return true;

                default:
                    return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // ARG HELPERS
        // ═══════════════════════════════════════════════════════════════

        private static void CompileOneArg(List<AstNodes.ExprNode> args, CompilerContext ctx)
        {
            if (args != null && args.Count > 0)
                args[0].Compile(ctx);
        }

        private static void CompileTwoArgs(List<AstNodes.ExprNode> args, CompilerContext ctx)
        {
            if (args != null && args.Count >= 2)
            {
                args[0].Compile(ctx);
                ctx.Emit(OpCode.PUSH, 0);
                args[1].Compile(ctx);
                ctx.Emit(OpCode.MOV, 1, 0);
                ctx.Emit(OpCode.POP, 0);
            }
        }

        private static void CompileFourArgs(List<AstNodes.ExprNode> args, CompilerContext ctx)
        {
            if (args == null || args.Count < 4) return;

            args[0].Compile(ctx);        // arg0 → R0
            ctx.Emit(OpCode.PUSH, 0);

            args[1].Compile(ctx);        // arg1 → R0
            ctx.Emit(OpCode.PUSH, 0);

            args[2].Compile(ctx);        // arg2 → R0
            ctx.Emit(OpCode.PUSH, 0);

            args[3].Compile(ctx);        // arg3 → R0
            ctx.Emit(OpCode.MOV, 3, 0);  // R0 → R3

            ctx.Emit(OpCode.POP, 0);
            ctx.Emit(OpCode.MOV, 2, 0);  // R0 → R2

            ctx.Emit(OpCode.POP, 0);
            ctx.Emit(OpCode.MOV, 1, 0);  // R0 → R1

            ctx.Emit(OpCode.POP, 0);     // arg0 → R0
        }

        private static void Emit(CompilerContext ctx, ChessOp op, int line, string comment)
        {
            ctx.Emit(OpCode.CUSTOM_0 + (int)op, 0, 0, 0, line, comment);
        }

        public bool TryCompileMethodCall(string objectName, string methodName,
                                         List<AstNodes.ExprNode> args,
                                         CompilerContext ctx, int sourceLine) => false;

        public bool TryCompileObjectDecl(string typeName, string varName,
                                         List<AstNodes.ExprNode> constructorArgs,
                                         CompilerContext ctx, int sourceLine) => false;
    }
}
