// Copyright CodeGamified 2025-2026
// MIT License — Chess
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using CodeGamified.Time;
using Chess.Core;

namespace Chess.Game
{
    /// <summary>
    /// Match manager — turn-based two-player Chess.
    ///
    /// Turn flow:
    ///   1. Player code runs and calls move(fc,fr,tc,tr) or move(fc,fr,tc,tr,promo)
    ///   2. Legal move validation + execution
    ///   3. Check/checkmate/stalemate/draw detection
    ///   4. AI responds with its move (powered by Chess.Core.Searcher)
    ///   5. Check/checkmate/stalemate/draw detection
    ///   6. Next tick → player code runs again
    ///
    /// AI engine: Iterative deepening alpha-beta with transposition table,
    /// killer moves, history heuristic, LMR, quiescence search, and opening book.
    /// Powered by Sebastian Lague's Chess Coding Adventure engine.
    /// </summary>
    public class ChessMatchManager : MonoBehaviour
    {
        private ChessBoard _board;

        // Config
        private bool _autoRestart;
        private float _restartDelay;
        private int _aiDepth;

        // AI engine (Chess.Core)
        private Searcher _searcher;
        private OpeningBook _openingBook;
        private CancellationTokenSource _searchCts;

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

            // Initialize the Chess.Core searcher on the board's internal engine
            _searcher = new Searcher(board.Board);
        }

        /// <summary>
        /// Load an opening book from text content (Book.txt format).
        /// Call after Initialize, before StartMatch.
        /// </summary>
        public void LoadOpeningBook(string bookContent)
        {
            if (!string.IsNullOrEmpty(bookContent))
                _openingBook = new OpeningBook(bookContent);
        }

        public void StartMatch()
        {
            _board.Reset();
            _searcher.ClearForNewPosition();
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
        // BUILT-IN AI (Chess.Core Searcher — iterative deepening alpha-beta)
        // ═══════════════════════════════════════════════════════════════

        private void DoAITurn()
        {
            if (GameOver || _board.SideToMove != PieceColor.Black) return;

            var moves = _board.GetLegalMoves();
            if (moves.Count == 0) return;

            // Try opening book first
            if (_openingBook != null &&
                _openingBook.TryGetBookMove(_board.Board, out string bookMoveStr))
            {
                var bookMove = MoveUtility.GetMoveFromUCIName(bookMoveStr, _board.Board);
                var chessMove = _board.FromCoreMove(bookMove);
                ExecuteAndContinue(chessMove);
                return;
            }

            // Run a timed search on the main thread (blocks briefly)
            int thinkTimeMs = GetThinkTimeMs();
            _searchCts = new CancellationTokenSource();
            Task.Delay(thinkTimeMs, _searchCts.Token)
                .ContinueWith(_ => _searcher.EndSearch());

            _searcher.StartSearch();
            _searchCts.Cancel();

            var (bestMove, _) = _searcher.GetSearchResult();
            if (!bestMove.IsNull)
            {
                var chessMove = _board.FromCoreMove(bestMove);
                ExecuteAndContinue(chessMove);
            }
        }

        private int GetThinkTimeMs()
        {
            return _aiDepth switch
            {
                1 => 50,
                2 => 100,
                3 => 250,
                4 => 500,
                5 => 1000,
                _ => 200
            };
        }

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

            if (_board.IsStalemate() || _board.IsFiftyMoveRule ||
                _board.IsInsufficientMaterial() || _board.IsThreefoldRepetition)
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

        // ═══════════════════════════════════════════════════════════════
        // ENGINE AI BRIDGE (for script builtins)
        // ═══════════════════════════════════════════════════════════════

        private int _lastSearchScore;
        private int _lastSearchDepth;

        // Async search state
        private volatile bool _asyncSearchRunning;
        private volatile bool _asyncSearchComplete;
        private int _asyncSearchResultIdx;
        private int _asyncSearchResultEval;

        /// <summary>Is an async engine search currently in progress?</summary>
        public bool IsSearchRunning => _asyncSearchRunning;

        /// <summary>Is an async engine search result ready to collect?</summary>
        public bool IsSearchComplete => _asyncSearchComplete;

        /// <summary>
        /// Start a depth-limited engine search on a background thread.
        /// Call IsSearchComplete to poll, then CollectSearchResult to get the result.
        /// </summary>
        public void BeginEngineSearch(int maxDepth)
        {
            if (_asyncSearchRunning) return;

            maxDepth = System.Math.Clamp(maxDepth, 1, 30);
            _asyncSearchRunning = true;
            _asyncSearchComplete = false;

            Task.Run(() =>
            {
                var (bestMove, eval) = _searcher.SearchToDepth(maxDepth);
                _asyncSearchResultEval = eval;

                if (bestMove.IsNull)
                    _asyncSearchResultIdx = -1;
                else
                {
                    int idx = _board.IndexOfCoreMove(bestMove);
                    _asyncSearchResultIdx = idx >= 0 ? idx : 0;
                }

                _asyncSearchComplete = true;
                _asyncSearchRunning = false;
            });
        }

        /// <summary>
        /// Collect the result of a completed async search.
        /// Updates LastSearchScore / LastSearchDepth.
        /// Returns best move index.
        /// </summary>
        public int CollectSearchResult()
        {
            _asyncSearchComplete = false;
            _lastSearchScore = _asyncSearchResultEval;
            _lastSearchDepth = _searcher.CurrentDepth;
            return _asyncSearchResultIdx;
        }

        /// <summary>Static evaluation of current position (centipawns, side-to-move POV).</summary>
        public int EngineStaticEval() => _searcher.StaticEval();

        /// <summary>
        /// Evaluate position after making the i-th legal move.
        /// Returns negated eval (opponent's perspective → our perspective).
        /// </summary>
        public int EngineEvalMove(int moveIndex)
        {
            var moves = _board.GetLegalMoves();
            if (moveIndex < 0 || moveIndex >= moves.Count) return 0;

            var coreMove = _board.ToCoreMove(moves[moveIndex]);
            if (!coreMove.HasValue) return 0;

            _board.Board.MakeMove(coreMove.Value, inSearch: true);
            int eval = -_searcher.StaticEval();
            _board.Board.UnmakeMove(coreMove.Value, inSearch: true);
            return eval;
        }

        /// <summary>Centipawn value for a piece type (1=pawn..6=king).</summary>
        public static int EnginePieceValue(int pieceType)
        {
            return pieceType switch
            {
                1 => Evaluation.PawnValue,    // 100
                2 => Evaluation.KnightValue,  // 300
                3 => Evaluation.BishopValue,  // 320
                4 => Evaluation.RookValue,    // 500
                5 => Evaluation.QueenValue,   // 900
                6 => 0,                        // king — infinite / not traded
                _ => 0
            };
        }

        public int LastSearchScore => _lastSearchScore;
        public int LastSearchDepth => _lastSearchDepth;
    }
}
