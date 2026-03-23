// Copyright CodeGamified 2025-2026
// MIT License — Chess
//
// ChessBoard — powered by Sebastian Lague's Chess Coding Adventure engine.
// Wraps Chess.Core.Board (bitboard engine) while maintaining the same public
// API that the rest of the CodeGamified game layer expects.
using System.Collections.Generic;
using System.Linq;
using Chess.Core;
using CoreBoard = Chess.Core.Board;
using CoreMove = Chess.Core.Move;
using CorePiece = Chess.Core.Piece;

namespace Chess.Game
{
    /// <summary>
    /// A single chess move — from (col,row) to (col,row) with optional metadata.
    /// </summary>
    public struct ChessMove
    {
        public int FromCol, FromRow;
        public int ToCol, ToRow;
        public MoveFlags Flags;

        /// <summary>For pawn promotion: the piece type to promote to (Queen by default).</summary>
        public PieceType PromotionType;

        public ChessMove(int fc, int fr, int tc, int tr,
                         MoveFlags flags = MoveFlags.Normal,
                         PieceType promo = PieceType.None)
        {
            FromCol = fc; FromRow = fr;
            ToCol = tc; ToRow = tr;
            Flags = flags;
            PromotionType = promo;
        }

        public bool IsCapture    => (Flags & MoveFlags.Capture) != 0;
        public bool IsEnPassant  => (Flags & MoveFlags.EnPassant) != 0;
        public bool IsCastleK    => (Flags & MoveFlags.CastleKingside) != 0;
        public bool IsCastleQ    => (Flags & MoveFlags.CastleQueenside) != 0;
        public bool IsCastle     => IsCastleK || IsCastleQ;
        public bool IsPromotion  => (Flags & MoveFlags.Promotion) != 0;
        public bool IsDoublePush => (Flags & MoveFlags.DoublePawnPush) != 0;

        public override string ToString()
        {
            string f = $"{(char)('a' + FromCol)}{FromRow + 1}";
            string t = $"{(char)('a' + ToCol)}{ToRow + 1}";
            string extra = "";
            if (IsCastleK) extra = " O-O";
            else if (IsCastleQ) extra = " O-O-O";
            else if (IsEnPassant) extra = " e.p.";
            else if (IsPromotion) extra = $"={PromotionType}";
            return $"{f}{t}{extra}";
        }
    }

    [System.Flags]
    public enum MoveFlags
    {
        Normal           = 0,
        Capture          = 1 << 0,
        DoublePawnPush   = 1 << 1,
        EnPassant        = 1 << 2,
        CastleKingside   = 1 << 3,
        CastleQueenside  = 1 << 4,
        Promotion        = 1 << 5,
    }

    /// <summary>
    /// Full chess board powered by Chess.Core (Sebastian Lague's Chess Coding Adventure).
    /// Provides the same public API as the original ChessBoard implementation,
    /// but delegates to the high-performance bitboard-based engine internally.
    ///
    /// Coordinates: col 0-7 (a-h), row 0-7 (1-8).
    /// White (Player) at bottom (rows 0-1), Black (AI) at top (rows 6-7).
    /// </summary>
    public class ChessBoard
    {
        public const int Size = 8;

        // ── Core engine ──
        internal CoreBoard Board { get; private set; }
        private MoveGenerator _moveGen;

        // ── Castling rights ──
        public bool WhiteCanCastleK => Board.CurrentGameState.HasKingsideCastleRight(true);
        public bool WhiteCanCastleQ => Board.CurrentGameState.HasQueensideCastleRight(true);
        public bool BlackCanCastleK => Board.CurrentGameState.HasKingsideCastleRight(false);
        public bool BlackCanCastleQ => Board.CurrentGameState.HasQueensideCastleRight(false);

        // ── En passant ──
        public int EnPassantCol => Board.CurrentGameState.enPassantFile - 1;
        public int EnPassantRow
        {
            get
            {
                if (Board.CurrentGameState.enPassantFile == 0) return -1;
                return Board.IsWhiteToMove ? 5 : 2;
            }
        }

        // ── State tracking ──
        public int TotalMoves => Board.PlyCount;
        public int HalfMoveClock => Board.FiftyMoveCounter;
        public PieceColor SideToMove => Board.IsWhiteToMove ? PieceColor.White : PieceColor.Black;

        // ── Piece counts ──
        public int WhitePieces { get; private set; }
        public int BlackPieces { get; private set; }
        public int WhiteMaterial { get; private set; }
        public int BlackMaterial { get; private set; }

        // ── Cached legal moves ──
        private List<ChessMove> _cachedLegalMoves;
        private CoreMove[] _cachedCoreMoves;
        private bool _legalMovesCacheValid;

        public void Initialize()
        {
            Board = CoreBoard.CreateBoard();
            _moveGen = new MoveGenerator();
            _moveGen.promotionsToGenerate = MoveGenerator.PromotionMode.All;
            RecountPieces();
        }

        public void Reset()
        {
            Board.LoadStartPosition();
            InvalidateLegalMoveCache();
            RecountPieces();
        }

        /// <summary>Load position from FEN string.</summary>
        public void LoadPosition(string fen)
        {
            Board.LoadPosition(fen);
            InvalidateLegalMoveCache();
            RecountPieces();
        }

        /// <summary>Get current FEN string.</summary>
        public string CurrentFEN => FenUtility.CurrentFen(Board);

        // ═══════════════════════════════════════════════════════════════
        // COORDINATE CONVERSION
        // ═══════════════════════════════════════════════════════════════

        internal static int ToSquareIndex(int col, int row) => row * 8 + col;
        internal static int ColFromSquare(int sq) => sq & 7;
        internal static int RowFromSquare(int sq) => sq >> 3;

        // ═══════════════════════════════════════════════════════════════
        // PIECE CONVERSION
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Convert Chess.Core piece int to ChessPiece.</summary>
        internal static ChessPiece FromCorePiece(int corePiece)
        {
            if (corePiece == 0) return ChessPiece.Empty;
            // White: 1-6 → 1-6 (same), Black: 9-14 → 7-12
            return new ChessPiece(corePiece <= 6 ? corePiece : corePiece - 2);
        }

        /// <summary>Convert ChessPiece to Chess.Core piece int.</summary>
        internal static int ToCorePiece(ChessPiece piece)
        {
            if (piece.IsEmpty) return 0;
            // White: 1-6 → 1-6 (same), Black: 7-12 → 9-14
            return piece.Raw <= 6 ? piece.Raw : piece.Raw + 2;
        }

        // ═══════════════════════════════════════════════════════════════
        // MOVE CONVERSION
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Convert Chess.Core Move to ChessMove.</summary>
        internal ChessMove FromCoreMove(CoreMove move)
        {
            int fromCol = ColFromSquare(move.StartSquare);
            int fromRow = RowFromSquare(move.StartSquare);
            int toCol = ColFromSquare(move.TargetSquare);
            int toRow = RowFromSquare(move.TargetSquare);

            MoveFlags flags = MoveFlags.Normal;
            PieceType promoType = PieceType.None;

            int targetPiece = Board.Square[move.TargetSquare];
            if (targetPiece != CorePiece.None)
                flags |= MoveFlags.Capture;

            switch (move.MoveFlag)
            {
                case CoreMove.EnPassantCaptureFlag:
                    flags |= MoveFlags.EnPassant | MoveFlags.Capture;
                    break;
                case CoreMove.CastleFlag:
                    flags |= (toCol > fromCol) ? MoveFlags.CastleKingside : MoveFlags.CastleQueenside;
                    break;
                case CoreMove.PawnTwoUpFlag:
                    flags |= MoveFlags.DoublePawnPush;
                    break;
                case CoreMove.PromoteToQueenFlag:
                    flags |= MoveFlags.Promotion;
                    promoType = PieceType.Queen;
                    if (targetPiece != CorePiece.None) flags |= MoveFlags.Capture;
                    break;
                case CoreMove.PromoteToKnightFlag:
                    flags |= MoveFlags.Promotion;
                    promoType = PieceType.Knight;
                    if (targetPiece != CorePiece.None) flags |= MoveFlags.Capture;
                    break;
                case CoreMove.PromoteToRookFlag:
                    flags |= MoveFlags.Promotion;
                    promoType = PieceType.Rook;
                    if (targetPiece != CorePiece.None) flags |= MoveFlags.Capture;
                    break;
                case CoreMove.PromoteToBishopFlag:
                    flags |= MoveFlags.Promotion;
                    promoType = PieceType.Bishop;
                    if (targetPiece != CorePiece.None) flags |= MoveFlags.Capture;
                    break;
            }

            return new ChessMove(fromCol, fromRow, toCol, toRow, flags, promoType);
        }

        /// <summary>
        /// Find the Chess.Core Move matching a ChessMove from the cached legal moves.
        /// </summary>
        internal CoreMove? ToCoreMove(ChessMove move)
        {
            EnsureLegalMoveCache();

            int startSq = ToSquareIndex(move.FromCol, move.FromRow);
            int targetSq = ToSquareIndex(move.ToCol, move.ToRow);
            CoreMove? firstMatch = null;

            for (int i = 0; i < _cachedCoreMoves.Length; i++)
            {
                var m = _cachedCoreMoves[i];
                if (m.StartSquare != startSq || m.TargetSquare != targetSq)
                    continue;

                if (move.IsPromotion)
                {
                    int expectedFlag = move.PromotionType switch
                    {
                        PieceType.Queen => CoreMove.PromoteToQueenFlag,
                        PieceType.Rook => CoreMove.PromoteToRookFlag,
                        PieceType.Knight => CoreMove.PromoteToKnightFlag,
                        PieceType.Bishop => CoreMove.PromoteToBishopFlag,
                        _ => CoreMove.PromoteToQueenFlag
                    };
                    if (m.MoveFlag == expectedFlag) return m;
                    firstMatch ??= m;
                }
                else if (!m.IsPromotion)
                {
                    return m;
                }
            }

            return firstMatch;
        }

        // ═══════════════════════════════════════════════════════════════
        // BASIC QUERIES
        // ═══════════════════════════════════════════════════════════════

        public bool InBounds(int col, int row)
            => col >= 0 && col < Size && row >= 0 && row < Size;

        public ChessPiece GetCell(int col, int row)
        {
            if (!InBounds(col, row)) return ChessPiece.Empty;
            return FromCorePiece(Board.Square[ToSquareIndex(col, row)]);
        }

        public void GetKingPos(PieceColor color, out int col, out int row)
        {
            int idx = (color == PieceColor.White) ? CoreBoard.WhiteIndex : CoreBoard.BlackIndex;
            int sq = Board.KingSquare[idx];
            col = ColFromSquare(sq);
            row = RowFromSquare(sq);
        }

        // ═══════════════════════════════════════════════════════════════
        // ATTACK DETECTION (bitboard-powered)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Is the given square attacked by any piece of `attackerColor`?</summary>
        public bool IsAttacked(int col, int row, PieceColor attackerColor)
        {
            if (!InBounds(col, row)) return false;
            return IsSquareAttacked(ToSquareIndex(col, row), attackerColor);
        }

        private bool IsSquareAttacked(int square, PieceColor attackerColor)
        {
            int attackColor = (attackerColor == PieceColor.White) ? CorePiece.White : CorePiece.Black;
            ulong blockers = Board.AllPiecesBitboard;

            // Orthogonal sliders (rook/queen)
            ulong orthoSliders = Board.PieceBitboards[CorePiece.MakePiece(CorePiece.Rook, attackColor)]
                               | Board.PieceBitboards[CorePiece.MakePiece(CorePiece.Queen, attackColor)];
            if (orthoSliders != 0)
            {
                ulong rookAttacks = Magic.GetRookAttacks(square, blockers);
                if ((rookAttacks & orthoSliders) != 0) return true;
            }

            // Diagonal sliders (bishop/queen)
            ulong diagSliders = Board.PieceBitboards[CorePiece.MakePiece(CorePiece.Bishop, attackColor)]
                              | Board.PieceBitboards[CorePiece.MakePiece(CorePiece.Queen, attackColor)];
            if (diagSliders != 0)
            {
                ulong bishopAttacks = Magic.GetBishopAttacks(square, blockers);
                if ((bishopAttacks & diagSliders) != 0) return true;
            }

            // Knights
            ulong knights = Board.PieceBitboards[CorePiece.MakePiece(CorePiece.Knight, attackColor)];
            if ((BitBoardUtility.KnightAttacks[square] & knights) != 0) return true;

            // Pawns (use reverse-perspective masks)
            ulong pawns = Board.PieceBitboards[CorePiece.MakePiece(CorePiece.Pawn, attackColor)];
            ulong pawnAttackMask = (attackerColor == PieceColor.White)
                ? BitBoardUtility.BlackPawnAttacks[square]
                : BitBoardUtility.WhitePawnAttacks[square];
            if ((pawnAttackMask & pawns) != 0) return true;

            // King
            ulong king = Board.PieceBitboards[CorePiece.MakePiece(CorePiece.King, attackColor)];
            if ((BitBoardUtility.KingMoves[square] & king) != 0) return true;

            return false;
        }

        /// <summary>Is the king of `color` currently in check?</summary>
        public bool IsInCheck(PieceColor color)
        {
            if (color == SideToMove)
                return Board.IsInCheck();

            int colorIdx = (color == PieceColor.White) ? CoreBoard.WhiteIndex : CoreBoard.BlackIndex;
            int kingSquare = Board.KingSquare[colorIdx];
            PieceColor enemy = (color == PieceColor.White) ? PieceColor.Black : PieceColor.White;
            return IsSquareAttacked(kingSquare, enemy);
        }

        // ═══════════════════════════════════════════════════════════════
        // LEGAL MOVE GENERATION
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Get all fully legal moves for the current side to move.
        /// Uses caching — invalidated after each move.
        /// </summary>
        public List<ChessMove> GetLegalMoves()
        {
            EnsureLegalMoveCache();
            return _cachedLegalMoves;
        }

        private void EnsureLegalMoveCache()
        {
            if (_legalMovesCacheValid && _cachedLegalMoves != null)
                return;

            var coreMoves = _moveGen.GenerateMoves(Board);
            _cachedCoreMoves = coreMoves.ToArray();
            _cachedLegalMoves = new List<ChessMove>(_cachedCoreMoves.Length);
            foreach (var m in _cachedCoreMoves)
                _cachedLegalMoves.Add(FromCoreMove(m));
            _legalMovesCacheValid = true;
        }

        /// <summary>Find the legal move list index of a Core Move (by value). Returns -1 if not found.</summary>
        internal int IndexOfCoreMove(CoreMove move)
        {
            EnsureLegalMoveCache();
            for (int i = 0; i < _cachedCoreMoves.Length; i++)
                if (_cachedCoreMoves[i].Value == move.Value) return i;
            return -1;
        }

        /// <summary>
        /// Get all fully legal moves for a specific color.
        /// Core engine generates moves for the current side, so for the other
        /// side a null-move switch is used.
        /// </summary>
        public List<ChessMove> GetLegalMovesFor(PieceColor color)
        {
            if (color == SideToMove)
                return GetLegalMoves();

            // Switch perspective via null move (only valid when not in check)
            if (!Board.IsInCheck())
            {
                Board.MakeNullMove();
                var coreMoves = _moveGen.GenerateMoves(Board);
                var result = new List<ChessMove>(coreMoves.Length);
                foreach (var m in coreMoves)
                    result.Add(FromCoreMove(m));
                Board.UnmakeNullMove();
                return result;
            }

            return new List<ChessMove>();
        }

        /// <summary>
        /// Generate all pseudo-legal moves for a color.
        /// Core engine generates fully legal moves, so this returns legal moves.
        /// Kept for API compatibility.
        /// </summary>
        public List<ChessMove> GeneratePseudoLegalMoves(PieceColor color)
        {
            return GetLegalMovesFor(color);
        }

        /// <summary>
        /// Check if a move (fc,fr→tc,tr) is in the legal move list for the current side.
        /// Optional promotionType for pawn promotion moves.
        /// </summary>
        public ChessMove? FindLegalMove(int fc, int fr, int tc, int tr,
                                         PieceType promotionType = PieceType.Queen)
        {
            var moves = GetLegalMoves();
            ChessMove? firstMatch = null;
            foreach (var m in moves)
            {
                if (m.FromCol == fc && m.FromRow == fr && m.ToCol == tc && m.ToRow == tr)
                {
                    if (m.IsPromotion)
                    {
                        if (m.PromotionType == promotionType) return m;
                        firstMatch ??= m;
                    }
                    else
                    {
                        return m;
                    }
                }
            }
            return firstMatch;
        }

        // ═══════════════════════════════════════════════════════════════
        // MOVE EXECUTION
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Execute a legal move, updating all board state.</summary>
        public bool ExecuteMove(ChessMove move)
        {
            var coreMove = ToCoreMove(move);
            if (!coreMove.HasValue) return false;

            Board.MakeMove(coreMove.Value);
            InvalidateLegalMoveCache();
            RecountPieces();
            return true;
        }

        /// <summary>Execute a move given as a Core Move directly (for AI integration).</summary>
        internal bool ExecuteCoreMove(CoreMove move)
        {
            Board.MakeMove(move);
            InvalidateLegalMoveCache();
            RecountPieces();
            return true;
        }

        // ═══════════════════════════════════════════════════════════════
        // GAME STATE QUERIES
        // ═══════════════════════════════════════════════════════════════

        public bool IsCheckmate()
        {
            return Board.IsInCheck() && GetLegalMoves().Count == 0;
        }

        public bool IsStalemate()
        {
            return !Board.IsInCheck() && GetLegalMoves().Count == 0;
        }

        public bool IsFiftyMoveRule => Board.FiftyMoveCounter >= 100;

        public bool IsThreefoldRepetition
        {
            get
            {
                int count = Board.RepetitionPositionHistory.Count(z => z == Board.ZobristKey);
                return count >= 3;
            }
        }

        public bool IsInsufficientMaterial()
        {
            return Arbiter.InsufficentMaterial(Board);
        }

        // ═══════════════════════════════════════════════════════════════
        // PIECE COUNTING
        // ═══════════════════════════════════════════════════════════════

        private void RecountPieces()
        {
            WhitePieces = 0; BlackPieces = 0;
            WhiteMaterial = 0; BlackMaterial = 0;

            for (int sq = 0; sq < 64; sq++)
            {
                int piece = Board.Square[sq];
                if (piece == CorePiece.None) continue;

                int type = CorePiece.PieceType(piece);
                int materialVal = type switch
                {
                    CorePiece.Pawn => 100,
                    CorePiece.Knight => 320,
                    CorePiece.Bishop => 330,
                    CorePiece.Rook => 500,
                    CorePiece.Queen => 900,
                    CorePiece.King => 20000,
                    _ => 0
                };

                if (CorePiece.IsWhite(piece))
                {
                    WhitePieces++;
                    WhiteMaterial += materialVal;
                }
                else
                {
                    BlackPieces++;
                    BlackMaterial += materialVal;
                }
            }
        }

        private void InvalidateLegalMoveCache()
        {
            _legalMovesCacheValid = false;
            _cachedLegalMoves = null;
            _cachedCoreMoves = null;
        }

        // ═══════════════════════════════════════════════════════════════
        // UTILITY
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Count how many squares a color attacks.</summary>
        public int CountAttackedSquares(PieceColor color)
        {
            int count = 0;
            for (int sq = 0; sq < 64; sq++)
                if (IsSquareAttacked(sq, color)) count++;
            return count;
        }

        // ═══════════════════════════════════════════════════════════════
        // STATE RESTORE (API compatibility — used to restore from save)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Restore full board state from saved snapshot.
        /// Builds a FEN and loads it on the core engine.
        /// </summary>
        public void RestoreState(ChessPiece[,] cells,
                                 bool wck, bool wcq, bool bck, bool bcq,
                                 int epCol, int epRow, int halfMove, int totalMoves,
                                 PieceColor side,
                                 int wkc, int wkr, int bkc, int bkr,
                                 int wp, int bp, int wm, int bm)
        {
            var sb = new System.Text.StringBuilder();

            // Piece placement
            for (int row = 7; row >= 0; row--)
            {
                int empty = 0;
                for (int col = 0; col < 8; col++)
                {
                    var p = cells[col, row];
                    if (p.IsEmpty)
                    {
                        empty++;
                    }
                    else
                    {
                        if (empty > 0) { sb.Append(empty); empty = 0; }
                        char c = p.Type switch
                        {
                            PieceType.Pawn => 'P', PieceType.Knight => 'N',
                            PieceType.Bishop => 'B', PieceType.Rook => 'R',
                            PieceType.Queen => 'Q', PieceType.King => 'K',
                            _ => '?'
                        };
                        sb.Append(p.IsBlack ? char.ToLower(c) : c);
                    }
                }
                if (empty > 0) sb.Append(empty);
                if (row > 0) sb.Append('/');
            }

            sb.Append(side == PieceColor.White ? " w " : " b ");

            string castling = "";
            if (wck) castling += "K";
            if (wcq) castling += "Q";
            if (bck) castling += "k";
            if (bcq) castling += "q";
            sb.Append(castling.Length > 0 ? castling : "-");

            sb.Append(' ');
            if (epCol >= 0 && epRow >= 0)
                sb.Append($"{(char)('a' + epCol)}{epRow + 1}");
            else
                sb.Append('-');

            sb.Append($" {halfMove} {totalMoves / 2 + 1}");

            Board.LoadPosition(sb.ToString());
            InvalidateLegalMoveCache();
            RecountPieces();
        }
    }
}
