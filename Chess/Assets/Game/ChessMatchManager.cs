// Copyright CodeGamified 2025-2026
// MIT License — Chess
using System.Collections.Generic;
using UnityEngine;
using CodeGamified.Time;

namespace Chess.Game
{
    /// <summary>
    /// Match manager — turn-based two-player Chess.
    ///
    /// Turn flow:
    ///   1. Player code runs and calls move(fc,fr,tc,tr) or move(fc,fr,tc,tr,promo)
    ///   2. Legal move validation + execution
    ///   3. Check/checkmate/stalemate/draw detection
    ///   4. AI responds with its move
    ///   5. Check/checkmate/stalemate/draw detection
    ///   6. Next tick → player code runs again
    ///
    /// Built-in AI: 2-ply minimax with alpha-beta pruning.
    /// Evaluation: material + piece-square tables + mobility.
    /// </summary>
    public class ChessMatchManager : MonoBehaviour
    {
        private ChessBoard _board;

        // Config
        private bool _autoRestart;
        private float _restartDelay;
        private int _aiDepth;

        // State
        public int PlayerWins { get; private set; }
        public int AIWins { get; private set; }
        public int Draws { get; private set; }
        public int MatchesPlayed { get; private set; }
        public bool GameOver { get; private set; }
        public bool MatchInProgress { get; private set; }
        public int Winner { get; private set; } // 0=none, 1=player(white), 2=AI(black), 3=draw

        public bool IsPlayerTurn => _board.SideToMove == PieceColor.White;
        public bool IsInCheck => _board.IsInCheck(_board.SideToMove);

        /// <summary>When true, the built-in C# AI is disabled and a script drives Black's moves.</summary>
        public bool UseScriptAI { get; set; }

        public int LastFromCol { get; private set; } = -1;
        public int LastFromRow { get; private set; } = -1;
        public int LastToCol { get; private set; } = -1;
        public int LastToRow { get; private set; } = -1;

        /// <summary>Promotion type for the next pawn move. Default: Queen.</summary>
        public PieceType PromotionChoice { get; set; } = PieceType.Queen;

        // Accessors
        public ChessBoard Board => _board;

        // Events
        public System.Action OnMatchStarted;
        public System.Action OnGameOver;
        public System.Action<int> OnWin;                // winner (1=player, 2=AI)
        public System.Action OnDraw;
        public System.Action<ChessMove> OnMoveMade;     // the move that was played
        public System.Action OnCheck;                    // a side is now in check
        public System.Action OnBoardChanged;
        public System.Action OnTurnChanged;

        public void Initialize(ChessBoard board, bool autoRestart = true,
                               float restartDelay = 3f, int aiDepth = 3)
        {
            _board = board;
            _autoRestart = autoRestart;
            _restartDelay = restartDelay;
            _aiDepth = aiDepth;
        }

        public void StartMatch()
        {
            _board.Reset();
            GameOver = false;
            MatchInProgress = true;
            Winner = 0;
            PromotionChoice = PieceType.Queen;
            ClearLastMove();

            OnMatchStarted?.Invoke();
            OnBoardChanged?.Invoke();
        }

        private void ClearLastMove()
        {
            LastFromCol = -1; LastFromRow = -1;
            LastToCol = -1; LastToRow = -1;
        }

        // ═══════════════════════════════════════════════════════════════
        // PLAYER COMMAND (called by IOHandler)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Player (white) attempts a move. Returns 1 on success, 0 on failure.
        /// After a valid move, the AI immediately responds.
        /// </summary>
        public int DoPlayerMove(int fc, int fr, int tc, int tr)
        {
            if (!MatchInProgress || GameOver) return 0;
            if (_board.SideToMove != PieceColor.White) return 0;

            var promoType = PromotionChoice;
            if (promoType == PieceType.None) promoType = PieceType.Queen;

            var move = _board.FindLegalMove(fc, fr, tc, tr, promoType);
            if (!move.HasValue) return 0;

            return ExecuteAndContinue(move.Value);
        }

        /// <summary>
        /// Set the piece type for the next pawn promotion.
        /// 2=Knight, 3=Bishop, 4=Rook, 5=Queen (default).
        /// </summary>
        public void SetPromotion(int pieceTypeInt)
        {
            switch (pieceTypeInt)
            {
                case 2: PromotionChoice = PieceType.Knight; break;
                case 3: PromotionChoice = PieceType.Bishop; break;
                case 4: PromotionChoice = PieceType.Rook; break;
                default: PromotionChoice = PieceType.Queen; break;
            }
        }

        private int ExecuteAndContinue(ChessMove move)
        {
            bool wasWhiteTurn = _board.SideToMove == PieceColor.White;

            if (!_board.ExecuteMove(move))
                return 0;

            LastFromCol = move.FromCol; LastFromRow = move.FromRow;
            LastToCol = move.ToCol; LastToRow = move.ToRow;

            OnMoveMade?.Invoke(move);
            OnBoardChanged?.Invoke();

            // Check for end conditions
            if (CheckEndConditions())
                return 1;

            // Check notification
            if (_board.IsInCheck(_board.SideToMove))
                OnCheck?.Invoke();

            OnTurnChanged?.Invoke();

            // If player just moved, AI responds (unless script AI is active)
            if (wasWhiteTurn && !UseScriptAI)
                DoAITurn();

            return 1;
        }

        // ═══════════════════════════════════════════════════════════════
        // AI SCRIPT COMMAND (called by IOHandler when isAISide=true)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// AI script (Black) attempts a move. Returns 1 on success, 0 on failure.
        /// </summary>
        public int DoAIScriptMove(int fc, int fr, int tc, int tr)
        {
            if (!MatchInProgress || GameOver) return 0;
            if (_board.SideToMove != PieceColor.Black) return 0;

            var promoType = PromotionChoice;
            if (promoType == PieceType.None) promoType = PieceType.Queen;

            var move = _board.FindLegalMove(fc, fr, tc, tr, promoType);
            if (!move.HasValue) return 0;

            return ExecuteAndContinue(move.Value);
        }

        // ═══════════════════════════════════════════════════════════════
        // BUILT-IN AI (Minimax with Alpha-Beta)
        // ═══════════════════════════════════════════════════════════════

        private void DoAITurn()
        {
            if (GameOver || _board.SideToMove != PieceColor.Black) return;

            var moves = _board.GetLegalMoves();
            if (moves.Count == 0) return; // handled by CheckEndConditions

            ChessMove best = moves[0];
            int bestScore = int.MinValue;

            foreach (var move in moves)
            {
                SaveState();
                _board.ExecuteMove(move);
                int score = -Negamax(_aiDepth - 1, int.MinValue + 1, int.MaxValue - 1);
                UndoLastMove(move);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = move;
                }
            }

            ExecuteAndContinue(best);
        }

        /// <summary>
        /// Negamax with alpha-beta pruning.
        /// Score is from the perspective of the side to move.
        /// </summary>
        private int Negamax(int depth, int alpha, int beta)
        {
            if (depth <= 0)
                return EvaluateForSideToMove();

            var moves = _board.GetLegalMoves();

            if (moves.Count == 0)
            {
                if (_board.IsInCheck(_board.SideToMove))
                    return -100000 + (_aiDepth - depth); // checkmate (prefer faster mate)
                return 0; // stalemate
            }

            if (_board.IsFiftyMoveRule || _board.IsInsufficientMaterial())
                return 0; // draw

            // Move ordering: captures first, then promotions
            moves.Sort((a, b) => ScoreMovePriority(b) - ScoreMovePriority(a));

            foreach (var move in moves)
            {
                SaveState();
                _board.ExecuteMove(move);
                int score = -Negamax(depth - 1, -beta, -alpha);
                UndoLastMove(move);

                if (score >= beta)
                    return beta; // beta cutoff

                if (score > alpha)
                    alpha = score;
            }

            return alpha;
        }

        private int ScoreMovePriority(ChessMove move)
        {
            int score = 0;
            if (move.IsCapture) score += 1000;
            if (move.IsPromotion) score += 900;
            if (move.IsEnPassant) score += 500;
            return score;
        }

        /// <summary>
        /// Undo a move by re-doing the board state.
        /// We use a simplified approach: store and restore.
        /// For the AI's minimax, we need a proper undo. Since ChessBoard.ExecuteMove
        /// is destructive, we push/pop state via a stack.
        /// </summary>
        private struct BoardState
        {
            public ChessPiece[,] Cells;
            public bool WCK, WCQ, BCK, BCQ;
            public int EpCol, EpRow;
            public int HalfMove, TotalMoves;
            public PieceColor Side;
            public int WKC, WKR, BKC, BKR;
            public int WP, BP, WM, BM;
        }

        private readonly Stack<BoardState> _stateStack = new Stack<BoardState>();

        /// <summary>Save state before AI search move.</summary>
        private void SaveState()
        {
            var cells = new ChessPiece[ChessBoard.Size, ChessBoard.Size];
            for (int c = 0; c < ChessBoard.Size; c++)
                for (int r = 0; r < ChessBoard.Size; r++)
                    cells[c, r] = _board.GetCell(c, r);

            _board.GetKingPos(PieceColor.White, out int wkc, out int wkr);
            _board.GetKingPos(PieceColor.Black, out int bkc, out int bkr);

            _stateStack.Push(new BoardState
            {
                Cells = cells,
                WCK = _board.WhiteCanCastleK, WCQ = _board.WhiteCanCastleQ,
                BCK = _board.BlackCanCastleK, BCQ = _board.BlackCanCastleQ,
                EpCol = _board.EnPassantCol, EpRow = _board.EnPassantRow,
                HalfMove = _board.HalfMoveClock, TotalMoves = _board.TotalMoves,
                Side = _board.SideToMove,
                WKC = wkc, WKR = wkr, BKC = bkc, BKR = bkr,
                WP = _board.WhitePieces, BP = _board.BlackPieces,
                WM = _board.WhiteMaterial, BM = _board.BlackMaterial,
            });
        }

        /// <summary>Undo by restoring saved state. Used during AI search.</summary>
        private void UndoLastMove(ChessMove move)
        {
            // We need to use save/restore for correctness since ExecuteMove is complex.
            // But that's expensive. Instead, let's override the board's internal state.
            // This is a pragmatic approach: re-ExecuteMove is wrapped with SaveState/RestoreState.
            // Actually, we should save before ExecuteMove in the search...

            // This is a known complexity. For the first pass, we'll use save/restore.
            // The proper fix is to add undo to ChessBoard directly.
            if (_stateStack.Count > 0)
            {
                var s = _stateStack.Pop();
                RestoreBoardState(s);
            }
        }

        private void RestoreBoardState(BoardState s)
        {
            // We need internal access. Since ChessBoard is our code, we add a RestoreFrom method.
            _board.RestoreState(s.Cells, s.WCK, s.WCQ, s.BCK, s.BCQ,
                               s.EpCol, s.EpRow, s.HalfMove, s.TotalMoves, s.Side,
                               s.WKC, s.WKR, s.BKC, s.BKR,
                               s.WP, s.BP, s.WM, s.BM);
        }

        // ═══════════════════════════════════════════════════════════════
        // EVALUATION
        // ═══════════════════════════════════════════════════════════════

        private int EvaluateForSideToMove()
        {
            int score = Evaluate();
            return (_board.SideToMove == PieceColor.White) ? score : -score;
        }

        /// <summary>
        /// Evaluate position from White's perspective.
        /// Positive = good for White, negative = good for Black.
        /// </summary>
        private int Evaluate()
        {
            int score = 0;

            // Material
            score += _board.WhiteMaterial - _board.BlackMaterial;

            // Piece-square bonuses
            for (int c = 0; c < ChessBoard.Size; c++)
            {
                for (int r = 0; r < ChessBoard.Size; r++)
                {
                    var p = _board.GetCell(c, r);
                    if (p.IsEmpty) continue;

                    int psq = GetPieceSquareBonus(p, c, r);
                    if (p.IsWhite) score += psq;
                    else score -= psq;
                }
            }

            return score;
        }

        /// <summary>Simple piece-square table bonus (encourages development and center control).</summary>
        private static int GetPieceSquareBonus(ChessPiece piece, int col, int row)
        {
            // Flip row for black so tables are always from white's perspective
            int r = piece.IsWhite ? row : 7 - row;
            int c = col;

            switch (piece.Type)
            {
                case PieceType.Pawn:
                    return PawnTable[r * 8 + c];
                case PieceType.Knight:
                    return KnightTable[r * 8 + c];
                case PieceType.Bishop:
                    return BishopTable[r * 8 + c];
                case PieceType.Rook:
                    return RookTable[r * 8 + c];
                case PieceType.Queen:
                    return QueenTable[r * 8 + c];
                case PieceType.King:
                    return KingTable[r * 8 + c];
                default:
                    return 0;
            }
        }

        // Piece-square tables (from white's perspective, row 0 = rank 1)
        private static readonly int[] PawnTable = {
             0,  0,  0,  0,  0,  0,  0,  0,
             5, 10, 10,-20,-20, 10, 10,  5,
             5, -5,-10,  0,  0,-10, -5,  5,
             0,  0,  0, 20, 20,  0,  0,  0,
             5,  5, 10, 25, 25, 10,  5,  5,
            10, 10, 20, 30, 30, 20, 10, 10,
            50, 50, 50, 50, 50, 50, 50, 50,
             0,  0,  0,  0,  0,  0,  0,  0,
        };

        private static readonly int[] KnightTable = {
            -50,-40,-30,-30,-30,-30,-40,-50,
            -40,-20,  0,  5,  5,  0,-20,-40,
            -30,  5, 10, 15, 15, 10,  5,-30,
            -30,  0, 15, 20, 20, 15,  0,-30,
            -30,  5, 15, 20, 20, 15,  5,-30,
            -30,  0, 10, 15, 15, 10,  0,-30,
            -40,-20,  0,  0,  0,  0,-20,-40,
            -50,-40,-30,-30,-30,-30,-40,-50,
        };

        private static readonly int[] BishopTable = {
            -20,-10,-10,-10,-10,-10,-10,-20,
            -10,  5,  0,  0,  0,  0,  5,-10,
            -10, 10, 10, 10, 10, 10, 10,-10,
            -10,  0, 10, 10, 10, 10,  0,-10,
            -10,  5,  5, 10, 10,  5,  5,-10,
            -10,  0,  5, 10, 10,  5,  0,-10,
            -10,  0,  0,  0,  0,  0,  0,-10,
            -20,-10,-10,-10,-10,-10,-10,-20,
        };

        private static readonly int[] RookTable = {
             0,  0,  0,  5,  5,  0,  0,  0,
            -5,  0,  0,  0,  0,  0,  0, -5,
            -5,  0,  0,  0,  0,  0,  0, -5,
            -5,  0,  0,  0,  0,  0,  0, -5,
            -5,  0,  0,  0,  0,  0,  0, -5,
            -5,  0,  0,  0,  0,  0,  0, -5,
             5, 10, 10, 10, 10, 10, 10,  5,
             0,  0,  0,  0,  0,  0,  0,  0,
        };

        private static readonly int[] QueenTable = {
            -20,-10,-10, -5, -5,-10,-10,-20,
            -10,  0,  5,  0,  0,  0,  0,-10,
            -10,  5,  5,  5,  5,  5,  0,-10,
              0,  0,  5,  5,  5,  5,  0, -5,
             -5,  0,  5,  5,  5,  5,  0, -5,
            -10,  0,  5,  5,  5,  5,  0,-10,
            -10,  0,  0,  0,  0,  0,  0,-10,
            -20,-10,-10, -5, -5,-10,-10,-20,
        };

        private static readonly int[] KingTable = {
             20, 30, 10,  0,  0, 10, 30, 20,
             20, 20,  0,  0,  0,  0, 20, 20,
            -10,-20,-20,-20,-20,-20,-20,-10,
            -20,-30,-30,-40,-40,-30,-30,-20,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
        };

        // ═══════════════════════════════════════════════════════════════
        // GAME FLOW
        // ═══════════════════════════════════════════════════════════════

        private bool CheckEndConditions()
        {
            if (_board.IsCheckmate())
            {
                // The side to move is checkmated
                int winner = (_board.SideToMove == PieceColor.White) ? 2 : 1;
                EndGame(winner);
                return true;
            }

            if (_board.IsStalemate() || _board.IsFiftyMoveRule || _board.IsInsufficientMaterial())
            {
                EndGame(3); // draw
                return true;
            }

            return false;
        }

        private void EndGame(int winner)
        {
            GameOver = true;
            MatchInProgress = false;
            Winner = winner;
            MatchesPlayed++;

            if (winner == 1)
            {
                PlayerWins++;
                OnWin?.Invoke(1);
            }
            else if (winner == 2)
            {
                AIWins++;
                OnWin?.Invoke(2);
            }
            else
            {
                Draws++;
                OnDraw?.Invoke();
            }

            OnGameOver?.Invoke();
            OnBoardChanged?.Invoke();

            if (_autoRestart)
                StartCoroutine(RestartAfterDelay());
        }

        private System.Collections.IEnumerator RestartAfterDelay()
        {
            float waited = 0f;
            while (waited < _restartDelay)
            {
                float ts = SimulationTime.Instance?.timeScale ?? 1f;
                if (SimulationTime.Instance != null && SimulationTime.Instance.isPaused)
                {
                    yield return null;
                    continue;
                }
                waited += UnityEngine.Time.deltaTime * ts;
                yield return null;
            }
            StartMatch();
        }

        // ═══════════════════════════════════════════════════════════════
        // QUERY HELPERS (for IOHandler)
        // ═══════════════════════════════════════════════════════════════

        public int GetLegalMoveCount()
            => _board.GetLegalMoves().Count;

        public ChessMove? GetLegalMoveAt(int index)
        {
            var moves = _board.GetLegalMoves();
            if (index < 0 || index >= moves.Count) return null;
            return moves[index];
        }

        /// <summary>Can white castle kingside right now (has rights + path clear + not in check)?</summary>
        public bool CanCastleKingsideNow()
            => CanCastleKingsideNow(PieceColor.White);

        public bool CanCastleKingsideNow(PieceColor side)
        {
            var moves = _board.GetLegalMovesFor(side);
            foreach (var m in moves)
                if (m.IsCastleK) return true;
            return false;
        }

        /// <summary>Can white castle queenside right now?</summary>
        public bool CanCastleQueensideNow()
            => CanCastleQueensideNow(PieceColor.White);

        public bool CanCastleQueensideNow(PieceColor side)
        {
            var moves = _board.GetLegalMovesFor(side);
            foreach (var m in moves)
                if (m.IsCastleQ) return true;
            return false;
        }
    }
}
