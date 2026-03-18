// Copyright CodeGamified 2025-2026
// MIT License — Chess

namespace Chess.Game
{
    /// <summary>
    /// Piece types on the chess board.
    /// Encoded as color × type for compact board representation.
    ///
    /// Board encoding:
    ///   0         = Empty
    ///   1-6       = White (Player): Pawn, Knight, Bishop, Rook, Queen, King
    ///   7-12      = Black (AI):     Pawn, Knight, Bishop, Rook, Queen, King
    ///
    /// For bytecode API:
    ///   get_piece_type(c,r) returns PieceType (0-6)
    ///   get_piece_color(c,r) returns PieceColor (0-2)
    /// </summary>
    public enum PieceType
    {
        None   = 0,
        Pawn   = 1,
        Knight = 2,
        Bishop = 3,
        Rook   = 4,
        Queen  = 5,
        King   = 6,
    }

    public enum PieceColor
    {
        None  = 0,
        White = 1,  // Player
        Black = 2,  // AI
    }

    /// <summary>
    /// A piece on the board, encoded as a single byte-sized value.
    /// 0 = empty, 1-6 = white pieces, 7-12 = black pieces.
    /// </summary>
    public struct ChessPiece
    {
        public readonly byte Raw;

        public static readonly ChessPiece Empty = new ChessPiece(0);

        // White pieces (1-6)
        public static readonly ChessPiece WPawn   = new ChessPiece(1);
        public static readonly ChessPiece WKnight = new ChessPiece(2);
        public static readonly ChessPiece WBishop = new ChessPiece(3);
        public static readonly ChessPiece WRook   = new ChessPiece(4);
        public static readonly ChessPiece WQueen  = new ChessPiece(5);
        public static readonly ChessPiece WKing   = new ChessPiece(6);

        // Black pieces (7-12)
        public static readonly ChessPiece BPawn   = new ChessPiece(7);
        public static readonly ChessPiece BKnight = new ChessPiece(8);
        public static readonly ChessPiece BBishop = new ChessPiece(9);
        public static readonly ChessPiece BRook   = new ChessPiece(10);
        public static readonly ChessPiece BQueen  = new ChessPiece(11);
        public static readonly ChessPiece BKing   = new ChessPiece(12);

        public ChessPiece(byte raw) { Raw = raw; }
        public ChessPiece(int raw) { Raw = (byte)raw; }

        public bool IsEmpty => Raw == 0;
        public bool IsWhite => Raw >= 1 && Raw <= 6;
        public bool IsBlack => Raw >= 7 && Raw <= 12;

        public PieceColor Color
        {
            get
            {
                if (IsWhite) return PieceColor.White;
                if (IsBlack) return PieceColor.Black;
                return PieceColor.None;
            }
        }

        public PieceType Type
        {
            get
            {
                if (IsEmpty) return PieceType.None;
                if (IsWhite) return (PieceType)Raw;
                return (PieceType)(Raw - 6); // 7→1(Pawn), 8→2(Knight), etc.
            }
        }

        public bool IsColor(PieceColor c)
        {
            if (c == PieceColor.White) return IsWhite;
            if (c == PieceColor.Black) return IsBlack;
            return IsEmpty;
        }

        public bool IsEnemy(PieceColor friendlyColor)
        {
            if (friendlyColor == PieceColor.White) return IsBlack;
            if (friendlyColor == PieceColor.Black) return IsWhite;
            return false;
        }

        /// <summary>Create a piece of the given color and type.</summary>
        public static ChessPiece Create(PieceColor color, PieceType type)
        {
            if (color == PieceColor.White) return new ChessPiece((byte)type);
            if (color == PieceColor.Black) return new ChessPiece((byte)((int)type + 6));
            return Empty;
        }

        /// <summary>Material value for AI evaluation.</summary>
        public int MaterialValue
        {
            get
            {
                switch (Type)
                {
                    case PieceType.Pawn:   return 100;
                    case PieceType.Knight: return 320;
                    case PieceType.Bishop: return 330;
                    case PieceType.Rook:   return 500;
                    case PieceType.Queen:  return 900;
                    case PieceType.King:   return 20000;
                    default: return 0;
                }
            }
        }

        public override string ToString()
        {
            if (IsEmpty) return ".";
            string[] symbols = { ".", "P", "N", "B", "R", "Q", "K", "p", "n", "b", "r", "q", "k" };
            return symbols[Raw];
        }

        public static bool operator ==(ChessPiece a, ChessPiece b) => a.Raw == b.Raw;
        public static bool operator !=(ChessPiece a, ChessPiece b) => a.Raw != b.Raw;
        public override bool Equals(object obj) => obj is ChessPiece p && p.Raw == Raw;
        public override int GetHashCode() => Raw;
    }
}
