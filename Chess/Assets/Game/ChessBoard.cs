// Copyright CodeGamified 2025-2026
// MIT License — Chess
using System.Collections.Generic;

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
    /// Full chess board with complete rules.
    ///
    /// Standard chess on 8×8 board:
    ///   - All piece movements (pawn, knight, bishop, rook, queen, king)
    ///   - Castling (kingside and queenside) with full conditions
    ///   - En passant capture
    ///   - Pawn promotion (to queen, rook, bishop, or knight)
    ///   - Check, checkmate, and stalemate detection
    ///   - Legal move generation (only moves that don't leave king in check)
    ///   - 50-move rule and simple draw detection
    ///
    /// Coordinates: col 0-7 (a-h), row 0-7 (1-8).
    /// White (Player) at bottom (rows 0-1), Black (AI) at top (rows 6-7).
    /// </summary>
    public class ChessBoard
    {
        public const int Size = 8;

        private ChessPiece[,] _cells;

        // ── Castling rights ──
        public bool WhiteCanCastleK { get; private set; }
        public bool WhiteCanCastleQ { get; private set; }
        public bool BlackCanCastleK { get; private set; }
        public bool BlackCanCastleQ { get; private set; }

        // ── En passant ──
        /// <summary>Column where en passant capture is valid (-1 if none).</summary>
        public int EnPassantCol { get; private set; } = -1;
        /// <summary>Row of the en passant target square (the square the capturing pawn lands on).</summary>
        public int EnPassantRow { get; private set; } = -1;

        // ── State tracking ──
        public int TotalMoves { get; private set; }
        public int HalfMoveClock { get; private set; } // moves since last pawn move or capture
        public PieceColor SideToMove { get; private set; }

        // ── King positions (cached for fast check detection) ──
        private int _whiteKingCol, _whiteKingRow;
        private int _blackKingCol, _blackKingRow;

        // ── Piece counts ──
        public int WhitePieces { get; private set; }
        public int BlackPieces { get; private set; }
        public int WhiteMaterial { get; private set; }
        public int BlackMaterial { get; private set; }

        // ── Cached legal moves (invalidated on each move) ──
        private List<ChessMove> _cachedLegalMoves;
        private bool _legalMovesCacheValid;

        // ── Threefold repetition tracking ──
        private Dictionary<long, int> _positionHistory = new Dictionary<long, int>();

        public void Initialize()
        {
            _cells = new ChessPiece[Size, Size];
            Reset();
        }

        public void Reset()
        {
            // Clear board
            for (int c = 0; c < Size; c++)
                for (int r = 0; r < Size; r++)
                    _cells[c, r] = ChessPiece.Empty;

            // White pieces (row 0)
            _cells[0, 0] = ChessPiece.WRook;
            _cells[1, 0] = ChessPiece.WKnight;
            _cells[2, 0] = ChessPiece.WBishop;
            _cells[3, 0] = ChessPiece.WQueen;
            _cells[4, 0] = ChessPiece.WKing;
            _cells[5, 0] = ChessPiece.WBishop;
            _cells[6, 0] = ChessPiece.WKnight;
            _cells[7, 0] = ChessPiece.WRook;

            // White pawns (row 1)
            for (int c = 0; c < Size; c++)
                _cells[c, 1] = ChessPiece.WPawn;

            // Black pieces (row 7)
            _cells[0, 7] = ChessPiece.BRook;
            _cells[1, 7] = ChessPiece.BKnight;
            _cells[2, 7] = ChessPiece.BBishop;
            _cells[3, 7] = ChessPiece.BQueen;
            _cells[4, 7] = ChessPiece.BKing;
            _cells[5, 7] = ChessPiece.BBishop;
            _cells[6, 7] = ChessPiece.BKnight;
            _cells[7, 7] = ChessPiece.BRook;

            // Black pawns (row 6)
            for (int c = 0; c < Size; c++)
                _cells[c, 6] = ChessPiece.BPawn;

            // State
            WhiteCanCastleK = true;
            WhiteCanCastleQ = true;
            BlackCanCastleK = true;
            BlackCanCastleQ = true;
            EnPassantCol = -1;
            EnPassantRow = -1;
            TotalMoves = 0;
            HalfMoveClock = 0;
            SideToMove = PieceColor.White;

            _whiteKingCol = 4; _whiteKingRow = 0;
            _blackKingCol = 4; _blackKingRow = 7;

            RecountPieces();
            InvalidateLegalMoveCache();

            _positionHistory.Clear();
            RecordPosition();
        }

        // ═══════════════════════════════════════════════════════════════
        // BASIC QUERIES
        // ═══════════════════════════════════════════════════════════════

        public bool InBounds(int col, int row)
            => col >= 0 && col < Size && row >= 0 && row < Size;

        public ChessPiece GetCell(int col, int row)
            => InBounds(col, row) ? _cells[col, row] : ChessPiece.Empty;

        public void GetKingPos(PieceColor color, out int col, out int row)
        {
            if (color == PieceColor.White) { col = _whiteKingCol; row = _whiteKingRow; }
            else { col = _blackKingCol; row = _blackKingRow; }
        }

        // ═══════════════════════════════════════════════════════════════
        // ATTACK DETECTION
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Is the given square attacked by any piece of `attackerColor`?</summary>
        public bool IsAttacked(int col, int row, PieceColor attackerColor)
        {
            // Knight attacks
            int[][] knightOffsets = {
                new[]{-2,-1}, new[]{-2,1}, new[]{-1,-2}, new[]{-1,2},
                new[]{1,-2}, new[]{1,2}, new[]{2,-1}, new[]{2,1}
            };
            foreach (var off in knightOffsets)
            {
                int nc = col + off[0], nr = row + off[1];
                if (InBounds(nc, nr))
                {
                    var p = _cells[nc, nr];
                    if (p.IsColor(attackerColor) && p.Type == PieceType.Knight)
                        return true;
                }
            }

            // Pawn attacks
            int pawnDir = (attackerColor == PieceColor.White) ? -1 : 1;
            // Pawns attack from the attacker's perspective going forward
            // White pawns at row r attack row r+1. So if we're checking
            // if (col,row) is attacked by white, a white pawn at (col±1, row-1) attacks us.
            int pawnSourceRow = row - (attackerColor == PieceColor.White ? 1 : -1);
            for (int dc = -1; dc <= 1; dc += 2)
            {
                int pc = col + dc;
                if (InBounds(pc, pawnSourceRow))
                {
                    var p = _cells[pc, pawnSourceRow];
                    if (p.IsColor(attackerColor) && p.Type == PieceType.Pawn)
                        return true;
                }
            }

            // King attacks (adjacent squares)
            for (int dc = -1; dc <= 1; dc++)
            {
                for (int dr = -1; dr <= 1; dr++)
                {
                    if (dc == 0 && dr == 0) continue;
                    int kc = col + dc, kr = row + dr;
                    if (InBounds(kc, kr))
                    {
                        var p = _cells[kc, kr];
                        if (p.IsColor(attackerColor) && p.Type == PieceType.King)
                            return true;
                    }
                }
            }

            // Sliding pieces: rook/queen (orthogonal) and bishop/queen (diagonal)
            // Orthogonal (rook, queen)
            int[][] orthDirs = { new[]{1,0}, new[]{-1,0}, new[]{0,1}, new[]{0,-1} };
            foreach (var d in orthDirs)
            {
                if (SlidingAttack(col, row, d[0], d[1], attackerColor,
                                  PieceType.Rook, PieceType.Queen))
                    return true;
            }

            // Diagonal (bishop, queen)
            int[][] diagDirs = { new[]{1,1}, new[]{1,-1}, new[]{-1,1}, new[]{-1,-1} };
            foreach (var d in diagDirs)
            {
                if (SlidingAttack(col, row, d[0], d[1], attackerColor,
                                  PieceType.Bishop, PieceType.Queen))
                    return true;
            }

            return false;
        }

        private bool SlidingAttack(int col, int row, int dc, int dr,
                                   PieceColor attackerColor,
                                   PieceType type1, PieceType type2)
        {
            int c = col + dc, r = row + dr;
            while (InBounds(c, r))
            {
                var p = _cells[c, r];
                if (!p.IsEmpty)
                {
                    if (p.IsColor(attackerColor) && (p.Type == type1 || p.Type == type2))
                        return true;
                    return false; // blocked
                }
                c += dc;
                r += dr;
            }
            return false;
        }

        /// <summary>Is the king of `color` currently in check?</summary>
        public bool IsInCheck(PieceColor color)
        {
            GetKingPos(color, out int kc, out int kr);
            PieceColor enemy = (color == PieceColor.White) ? PieceColor.Black : PieceColor.White;
            return IsAttacked(kc, kr, enemy);
        }

        // ═══════════════════════════════════════════════════════════════
        // PSEUDO-LEGAL MOVE GENERATION
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Generate all pseudo-legal moves for a color (does NOT filter check).
        /// </summary>
        public List<ChessMove> GeneratePseudoLegalMoves(PieceColor color)
        {
            var moves = new List<ChessMove>(64);

            for (int c = 0; c < Size; c++)
            {
                for (int r = 0; r < Size; r++)
                {
                    var piece = _cells[c, r];
                    if (!piece.IsColor(color)) continue;

                    switch (piece.Type)
                    {
                        case PieceType.Pawn:   GenPawnMoves(c, r, color, moves); break;
                        case PieceType.Knight: GenKnightMoves(c, r, color, moves); break;
                        case PieceType.Bishop: GenSlidingMoves(c, r, color, moves, true, false); break;
                        case PieceType.Rook:   GenSlidingMoves(c, r, color, moves, false, true); break;
                        case PieceType.Queen:  GenSlidingMoves(c, r, color, moves, true, true); break;
                        case PieceType.King:   GenKingMoves(c, r, color, moves); break;
                    }
                }
            }

            return moves;
        }

        private void GenPawnMoves(int c, int r, PieceColor color, List<ChessMove> moves)
        {
            int dir = (color == PieceColor.White) ? 1 : -1;
            int startRow = (color == PieceColor.White) ? 1 : 6;
            int promoRow = (color == PieceColor.White) ? 7 : 0;

            // Single push
            int nr = r + dir;
            if (InBounds(c, nr) && _cells[c, nr].IsEmpty)
            {
                if (nr == promoRow)
                    AddPromotionMoves(c, r, c, nr, MoveFlags.Promotion, moves);
                else
                {
                    moves.Add(new ChessMove(c, r, c, nr));

                    // Double push from starting row
                    if (r == startRow)
                    {
                        int nr2 = r + 2 * dir;
                        if (_cells[c, nr2].IsEmpty)
                            moves.Add(new ChessMove(c, r, c, nr2, MoveFlags.DoublePawnPush));
                    }
                }
            }

            // Captures (including en passant)
            for (int dc = -1; dc <= 1; dc += 2)
            {
                int nc = c + dc;
                if (!InBounds(nc, nr)) continue;

                var target = _cells[nc, nr];
                PieceColor enemy = (color == PieceColor.White) ? PieceColor.Black : PieceColor.White;

                if (target.IsColor(enemy))
                {
                    if (nr == promoRow)
                        AddPromotionMoves(c, r, nc, nr, MoveFlags.Promotion | MoveFlags.Capture, moves);
                    else
                        moves.Add(new ChessMove(c, r, nc, nr, MoveFlags.Capture));
                }
                else if (nc == EnPassantCol && nr == EnPassantRow)
                {
                    moves.Add(new ChessMove(c, r, nc, nr, MoveFlags.EnPassant | MoveFlags.Capture));
                }
            }
        }

        private void AddPromotionMoves(int fc, int fr, int tc, int tr,
                                        MoveFlags baseFlags, List<ChessMove> moves)
        {
            moves.Add(new ChessMove(fc, fr, tc, tr, baseFlags, PieceType.Queen));
            moves.Add(new ChessMove(fc, fr, tc, tr, baseFlags, PieceType.Rook));
            moves.Add(new ChessMove(fc, fr, tc, tr, baseFlags, PieceType.Bishop));
            moves.Add(new ChessMove(fc, fr, tc, tr, baseFlags, PieceType.Knight));
        }

        private void GenKnightMoves(int c, int r, PieceColor color, List<ChessMove> moves)
        {
            int[][] offsets = {
                new[]{-2,-1}, new[]{-2,1}, new[]{-1,-2}, new[]{-1,2},
                new[]{1,-2}, new[]{1,2}, new[]{2,-1}, new[]{2,1}
            };
            foreach (var off in offsets)
            {
                int nc = c + off[0], nr = r + off[1];
                if (!InBounds(nc, nr)) continue;
                var target = _cells[nc, nr];
                if (target.IsColor(color)) continue; // can't capture own
                var flags = target.IsEmpty ? MoveFlags.Normal : MoveFlags.Capture;
                moves.Add(new ChessMove(c, r, nc, nr, flags));
            }
        }

        private void GenSlidingMoves(int c, int r, PieceColor color, List<ChessMove> moves,
                                     bool diagonal, bool orthogonal)
        {
            int[][] dirs;
            if (diagonal && orthogonal)
                dirs = new[] {
                    new[]{1,0}, new[]{-1,0}, new[]{0,1}, new[]{0,-1},
                    new[]{1,1}, new[]{1,-1}, new[]{-1,1}, new[]{-1,-1}
                };
            else if (diagonal)
                dirs = new[] {
                    new[]{1,1}, new[]{1,-1}, new[]{-1,1}, new[]{-1,-1}
                };
            else
                dirs = new[] {
                    new[]{1,0}, new[]{-1,0}, new[]{0,1}, new[]{0,-1}
                };

            foreach (var d in dirs)
            {
                int nc = c + d[0], nr = r + d[1];
                while (InBounds(nc, nr))
                {
                    var target = _cells[nc, nr];
                    if (target.IsColor(color)) break; // blocked by own piece
                    if (!target.IsEmpty)
                    {
                        moves.Add(new ChessMove(c, r, nc, nr, MoveFlags.Capture));
                        break; // blocked after capture
                    }
                    moves.Add(new ChessMove(c, r, nc, nr));
                    nc += d[0]; nr += d[1];
                }
            }
        }

        private void GenKingMoves(int c, int r, PieceColor color, List<ChessMove> moves)
        {
            // Normal king moves
            for (int dc = -1; dc <= 1; dc++)
            {
                for (int dr = -1; dr <= 1; dr++)
                {
                    if (dc == 0 && dr == 0) continue;
                    int nc = c + dc, nr = r + dr;
                    if (!InBounds(nc, nr)) continue;
                    var target = _cells[nc, nr];
                    if (target.IsColor(color)) continue;
                    var flags = target.IsEmpty ? MoveFlags.Normal : MoveFlags.Capture;
                    moves.Add(new ChessMove(c, r, nc, nr, flags));
                }
            }

            // Castling
            PieceColor enemy = (color == PieceColor.White) ? PieceColor.Black : PieceColor.White;
            int row = (color == PieceColor.White) ? 0 : 7;
            if (r != row || c != 4) return; // king not on starting square
            if (IsAttacked(c, r, enemy)) return; // can't castle out of check

            bool canK = (color == PieceColor.White) ? WhiteCanCastleK : BlackCanCastleK;
            bool canQ = (color == PieceColor.White) ? WhiteCanCastleQ : BlackCanCastleQ;

            // Kingside: king e→g, rook h→f
            if (canK && _cells[5, row].IsEmpty && _cells[6, row].IsEmpty
                && _cells[7, row].Type == PieceType.Rook && _cells[7, row].IsColor(color)
                && !IsAttacked(5, row, enemy) && !IsAttacked(6, row, enemy))
            {
                moves.Add(new ChessMove(4, row, 6, row, MoveFlags.CastleKingside));
            }

            // Queenside: king e→c, rook a→d
            if (canQ && _cells[3, row].IsEmpty && _cells[2, row].IsEmpty && _cells[1, row].IsEmpty
                && _cells[0, row].Type == PieceType.Rook && _cells[0, row].IsColor(color)
                && !IsAttacked(3, row, enemy) && !IsAttacked(2, row, enemy))
            {
                moves.Add(new ChessMove(4, row, 2, row, MoveFlags.CastleQueenside));
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // LEGAL MOVE GENERATION (filters out moves that leave king in check)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Get all fully legal moves for the current side to move.
        /// Uses caching — call InvalidateLegalMoveCache() after state changes.
        /// </summary>
        public List<ChessMove> GetLegalMoves()
        {
            if (_legalMovesCacheValid && _cachedLegalMoves != null)
                return _cachedLegalMoves;

            _cachedLegalMoves = GetLegalMovesFor(SideToMove);
            _legalMovesCacheValid = true;
            return _cachedLegalMoves;
        }

        /// <summary>Get all fully legal moves for a specific color.</summary>
        public List<ChessMove> GetLegalMovesFor(PieceColor color)
        {
            var pseudo = GeneratePseudoLegalMoves(color);
            var legal = new List<ChessMove>(pseudo.Count);

            foreach (var move in pseudo)
            {
                if (IsMoveLegal(move, color))
                    legal.Add(move);
            }

            return legal;
        }

        /// <summary>Test if a pseudo-legal move is truly legal (doesn't leave own king in check).</summary>
        private bool IsMoveLegal(ChessMove move, PieceColor color)
        {
            // Make the move on a temporary basis
            var undo = MakeMoveFast(move);
            PieceColor enemy = (color == PieceColor.White) ? PieceColor.Black : PieceColor.White;
            GetKingPos(color, out int kc, out int kr);
            bool inCheck = IsAttacked(kc, kr, enemy);
            UndoMoveFast(move, undo);
            return !inCheck;
        }

        // ═══════════════════════════════════════════════════════════════
        // MOVE EXECUTION — FAST (for legality testing, no state updates)
        // ═══════════════════════════════════════════════════════════════

        private struct UndoInfo
        {
            public ChessPiece CapturedPiece;
            public int OldWhiteKingCol, OldWhiteKingRow;
            public int OldBlackKingCol, OldBlackKingRow;
        }

        private UndoInfo MakeMoveFast(ChessMove move)
        {
            var undo = new UndoInfo
            {
                CapturedPiece = _cells[move.ToCol, move.ToRow],
                OldWhiteKingCol = _whiteKingCol, OldWhiteKingRow = _whiteKingRow,
                OldBlackKingCol = _blackKingCol, OldBlackKingRow = _blackKingRow,
            };

            var piece = _cells[move.FromCol, move.FromRow];

            // Handle en passant capture
            if (move.IsEnPassant)
            {
                int capturedPawnRow = (piece.Color == PieceColor.White) ? move.ToRow - 1 : move.ToRow + 1;
                undo.CapturedPiece = _cells[move.ToCol, capturedPawnRow];
                _cells[move.ToCol, capturedPawnRow] = ChessPiece.Empty;
            }

            // Handle castling rook movement
            if (move.IsCastleK)
            {
                int row = move.FromRow;
                _cells[5, row] = _cells[7, row];
                _cells[7, row] = ChessPiece.Empty;
            }
            else if (move.IsCastleQ)
            {
                int row = move.FromRow;
                _cells[3, row] = _cells[0, row];
                _cells[0, row] = ChessPiece.Empty;
            }

            // Move piece
            _cells[move.ToCol, move.ToRow] = piece;
            _cells[move.FromCol, move.FromRow] = ChessPiece.Empty;

            // Handle promotion
            if (move.IsPromotion)
            {
                _cells[move.ToCol, move.ToRow] = ChessPiece.Create(piece.Color, move.PromotionType);
            }

            // Update king position
            if (piece.Type == PieceType.King)
            {
                if (piece.IsWhite) { _whiteKingCol = move.ToCol; _whiteKingRow = move.ToRow; }
                else { _blackKingCol = move.ToCol; _blackKingRow = move.ToRow; }
            }

            return undo;
        }

        private void UndoMoveFast(ChessMove move, UndoInfo undo)
        {
            var piece = _cells[move.ToCol, move.ToRow];

            // Undo promotion
            if (move.IsPromotion)
            {
                piece = ChessPiece.Create(piece.Color, PieceType.Pawn);
            }

            // Move piece back
            _cells[move.FromCol, move.FromRow] = piece;

            // Restore captured piece
            if (move.IsEnPassant)
            {
                _cells[move.ToCol, move.ToRow] = ChessPiece.Empty;
                int capturedPawnRow = (piece.Color == PieceColor.White) ? move.ToRow - 1 : move.ToRow + 1;
                _cells[move.ToCol, capturedPawnRow] = undo.CapturedPiece;
            }
            else
            {
                _cells[move.ToCol, move.ToRow] = undo.CapturedPiece;
            }

            // Undo castling rook
            if (move.IsCastleK)
            {
                int row = move.FromRow;
                _cells[7, row] = _cells[5, row];
                _cells[5, row] = ChessPiece.Empty;
            }
            else if (move.IsCastleQ)
            {
                int row = move.FromRow;
                _cells[0, row] = _cells[3, row];
                _cells[3, row] = ChessPiece.Empty;
            }

            // Restore king positions
            _whiteKingCol = undo.OldWhiteKingCol;
            _whiteKingRow = undo.OldWhiteKingRow;
            _blackKingCol = undo.OldBlackKingCol;
            _blackKingRow = undo.OldBlackKingRow;
        }

        // ═══════════════════════════════════════════════════════════════
        // MOVE EXECUTION — FULL (updates all state)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Execute a legal move, updating all board state.
        /// Returns true if the move was executed.
        /// Caller should verify legality first.
        /// </summary>
        public bool ExecuteMove(ChessMove move)
        {
            var piece = _cells[move.FromCol, move.FromRow];
            if (piece.IsEmpty) return false;

            bool isCapture = move.IsCapture;
            bool isPawnMove = piece.Type == PieceType.Pawn;

            // Handle en passant capture
            if (move.IsEnPassant)
            {
                int capturedPawnRow = (piece.Color == PieceColor.White) ? move.ToRow - 1 : move.ToRow + 1;
                _cells[move.ToCol, capturedPawnRow] = ChessPiece.Empty;
            }

            // Handle castling rook movement
            if (move.IsCastleK)
            {
                int row = move.FromRow;
                _cells[5, row] = _cells[7, row];
                _cells[7, row] = ChessPiece.Empty;
            }
            else if (move.IsCastleQ)
            {
                int row = move.FromRow;
                _cells[3, row] = _cells[0, row];
                _cells[0, row] = ChessPiece.Empty;
            }

            // Move piece
            _cells[move.ToCol, move.ToRow] = piece;
            _cells[move.FromCol, move.FromRow] = ChessPiece.Empty;

            // Handle promotion
            if (move.IsPromotion)
            {
                var promoType = move.PromotionType != PieceType.None ? move.PromotionType : PieceType.Queen;
                _cells[move.ToCol, move.ToRow] = ChessPiece.Create(piece.Color, promoType);
            }

            // Update king position
            if (piece.Type == PieceType.King)
            {
                if (piece.IsWhite) { _whiteKingCol = move.ToCol; _whiteKingRow = move.ToRow; }
                else { _blackKingCol = move.ToCol; _blackKingRow = move.ToRow; }
            }

            // Update castling rights
            UpdateCastlingRights(move, piece);

            // Update en passant
            if (move.IsDoublePush)
            {
                EnPassantCol = move.ToCol;
                EnPassantRow = (piece.Color == PieceColor.White) ? move.ToRow - 1 : move.ToRow + 1;
            }
            else
            {
                EnPassantCol = -1;
                EnPassantRow = -1;
            }

            // Update half-move clock (50-move rule)
            if (isPawnMove || isCapture)
                HalfMoveClock = 0;
            else
                HalfMoveClock++;

            TotalMoves++;

            // Switch side to move
            SideToMove = (SideToMove == PieceColor.White) ? PieceColor.Black : PieceColor.White;

            RecountPieces();
            InvalidateLegalMoveCache();
            RecordPosition();
            return true;
        }

        private void UpdateCastlingRights(ChessMove move, ChessPiece piece)
        {
            // King moved
            if (piece.Type == PieceType.King)
            {
                if (piece.IsWhite) { WhiteCanCastleK = false; WhiteCanCastleQ = false; }
                else { BlackCanCastleK = false; BlackCanCastleQ = false; }
            }

            // Rook moved or captured
            if (move.FromCol == 0 && move.FromRow == 0) WhiteCanCastleQ = false;
            if (move.FromCol == 7 && move.FromRow == 0) WhiteCanCastleK = false;
            if (move.FromCol == 0 && move.FromRow == 7) BlackCanCastleQ = false;
            if (move.FromCol == 7 && move.FromRow == 7) BlackCanCastleK = false;

            // Rook captured on its starting square
            if (move.ToCol == 0 && move.ToRow == 0) WhiteCanCastleQ = false;
            if (move.ToCol == 7 && move.ToRow == 0) WhiteCanCastleK = false;
            if (move.ToCol == 0 && move.ToRow == 7) BlackCanCastleQ = false;
            if (move.ToCol == 7 && move.ToRow == 7) BlackCanCastleK = false;
        }

        // ═══════════════════════════════════════════════════════════════
        // GAME STATE QUERIES
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Is the current side to move in checkmate?</summary>
        public bool IsCheckmate()
        {
            if (!IsInCheck(SideToMove)) return false;
            return GetLegalMoves().Count == 0;
        }

        /// <summary>Is the current side to move in stalemate (no legal moves, not in check)?</summary>
        public bool IsStalemate()
        {
            if (IsInCheck(SideToMove)) return false;
            return GetLegalMoves().Count == 0;
        }

        /// <summary>50-move rule draw.</summary>
        public bool IsFiftyMoveRule => HalfMoveClock >= 100; // 50 full moves = 100 half moves

        /// <summary>Threefold repetition draw (same position occurred 3+ times).</summary>
        public bool IsThreefoldRepetition
        {
            get
            {
                long hash = ComputePositionHash();
                return _positionHistory.TryGetValue(hash, out int count) && count >= 3;
            }
        }

        /// <summary>Insufficient material draw (K vs K, K+B vs K, K+N vs K).</summary>
        public bool IsInsufficientMaterial()
        {
            if (WhitePieces > 2 || BlackPieces > 2) return false;
            // Both sides have at most 1 piece each besides king
            // Check if any pawns, rooks, or queens remain
            for (int c = 0; c < Size; c++)
            {
                for (int r = 0; r < Size; r++)
                {
                    var p = _cells[c, r];
                    if (p.IsEmpty) continue;
                    if (p.Type == PieceType.Pawn || p.Type == PieceType.Rook ||
                        p.Type == PieceType.Queen)
                        return false;
                }
            }
            return true; // only kings, bishops, and/or knights with ≤2 pieces per side
        }

        /// <summary>
        /// Check if a move (fc,fr→tc,tr) is in the legal move list for the current side.
        /// Optional promotionType for pawn promotion moves.
        /// </summary>
        public ChessMove? FindLegalMove(int fc, int fr, int tc, int tr,
                                         PieceType promotionType = PieceType.Queen)
        {
            var moves = GetLegalMoves();
            foreach (var m in moves)
            {
                if (m.FromCol == fc && m.FromRow == fr && m.ToCol == tc && m.ToRow == tr)
                {
                    // For promotion moves, match the promotion type
                    if (m.IsPromotion)
                    {
                        if (m.PromotionType == promotionType)
                            return m;
                    }
                    else
                    {
                        return m;
                    }
                }
            }
            // If promotion type didn't match, return first matching move
            foreach (var m in moves)
            {
                if (m.FromCol == fc && m.FromRow == fr && m.ToCol == tc && m.ToRow == tr)
                    return m;
            }
            return null;
        }

        // ═══════════════════════════════════════════════════════════════
        // PIECE COUNTING
        // ═══════════════════════════════════════════════════════════════

        private void RecountPieces()
        {
            WhitePieces = 0; BlackPieces = 0;
            WhiteMaterial = 0; BlackMaterial = 0;

            for (int c = 0; c < Size; c++)
            {
                for (int r = 0; r < Size; r++)
                {
                    var p = _cells[c, r];
                    if (p.IsWhite) { WhitePieces++; WhiteMaterial += p.MaterialValue; }
                    else if (p.IsBlack) { BlackPieces++; BlackMaterial += p.MaterialValue; }
                }
            }
        }

        private void InvalidateLegalMoveCache()
        {
            _legalMovesCacheValid = false;
            _cachedLegalMoves = null;
        }

        // ═══════════════════════════════════════════════════════════════
        // UTILITY
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Count how many squares a color attacks (for mobility evaluation).</summary>
        public int CountAttackedSquares(PieceColor color)
        {
            int count = 0;
            for (int c = 0; c < Size; c++)
                for (int r = 0; r < Size; r++)
                    if (IsAttacked(c, r, color)) count++;
            return count;
        }

        // ═══════════════════════════════════════════════════════════════
        // POSITION HASHING (threefold repetition)
        // ═══════════════════════════════════════════════════════════════

        private long ComputePositionHash()
        {
            unchecked
            {
                long h = 17;
                for (int c = 0; c < Size; c++)
                    for (int r = 0; r < Size; r++)
                        h = h * 31 + _cells[c, r].Raw;

                h = h * 31 + (int)SideToMove;
                h = h * 31 + (WhiteCanCastleK ? 1 : 0);
                h = h * 31 + (WhiteCanCastleQ ? 2 : 0);
                h = h * 31 + (BlackCanCastleK ? 4 : 0);
                h = h * 31 + (BlackCanCastleQ ? 8 : 0);
                h = h * 31 + (EnPassantCol + 1);
                return h;
            }
        }

        private void RecordPosition()
        {
            long hash = ComputePositionHash();
            if (_positionHistory.TryGetValue(hash, out int count))
                _positionHistory[hash] = count + 1;
            else
                _positionHistory[hash] = 1;
        }

        // ═══════════════════════════════════════════════════════════════
        // STATE RESTORE (used by AI search undo)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Restore full board state from saved snapshot. Used by ChessMatchManager
        /// for AI minimax undo.
        /// </summary>
        public void RestoreState(ChessPiece[,] cells,
                                 bool wck, bool wcq, bool bck, bool bcq,
                                 int epCol, int epRow, int halfMove, int totalMoves,
                                 PieceColor side,
                                 int wkc, int wkr, int bkc, int bkr,
                                 int wp, int bp, int wm, int bm)
        {
            for (int c = 0; c < Size; c++)
                for (int r = 0; r < Size; r++)
                    _cells[c, r] = cells[c, r];

            WhiteCanCastleK = wck; WhiteCanCastleQ = wcq;
            BlackCanCastleK = bck; BlackCanCastleQ = bcq;
            EnPassantCol = epCol; EnPassantRow = epRow;
            HalfMoveClock = halfMove; TotalMoves = totalMoves;
            SideToMove = side;
            _whiteKingCol = wkc; _whiteKingRow = wkr;
            _blackKingCol = bkc; _blackKingRow = bkr;
            WhitePieces = wp; BlackPieces = bp;
            WhiteMaterial = wm; BlackMaterial = bm;
            InvalidateLegalMoveCache();
        }
    }
}
