// Copyright CodeGamified 2025-2026
// MIT License — Chess
using UnityEngine;
using CodeGamified.Quality;

namespace Chess.Game
{
    /// <summary>
    /// Visual renderer for the Chess board.
    /// 8×8 on XY plane: col→X, row→Y.
    /// Light/dark alternating squares.
    /// Pieces rendered as scaled primitives with color-coded materials:
    ///   - White pieces: bright cream/gold with emission
    ///   - Black pieces: dark charcoal/purple with emission
    /// Piece shape indicated by height/scale:
    ///   Pawn=short cylinder, Knight=medium sphere, Bishop=tall narrow cylinder,
    ///   Rook=medium cube, Queen=tall sphere, King=tallest sphere with capsule crown.
    /// </summary>
    public class ChessRenderer : MonoBehaviour, IQualityResponsive
    {
        private ChessBoard _board;
        private float _cellSize;

        private GameObject[,] _squareVisuals;
        private GameObject[,] _pieceVisuals;
        private float _originX;
        private float _originY;

        // Square colors
        private static readonly Color LightSquare = new Color(0.85f, 0.82f, 0.72f);
        private static readonly Color DarkSquare  = new Color(0.42f, 0.30f, 0.20f);

        // Piece colors (neon-ish CodeGamified aesthetic)
        private static readonly Color WhiteColor = new Color(1.0f, 0.92f, 0.65f);  // warm cream-gold
        private static readonly Color BlackColor = new Color(0.35f, 0.25f, 0.50f);  // dark purple

        // Last-move highlight
        private static readonly Color HighlightColor = new Color(0.3f, 0.8f, 0.3f, 0.4f);

        public void Initialize(ChessBoard board, float cellSize = 1.0f)
        {
            _board = board;
            _cellSize = cellSize;

            _originX = -(board.Size * cellSize) * 0.5f + cellSize * 0.5f;
            _originY = cellSize * 0.5f;

            BuildSquares();
            _pieceVisuals = new GameObject[ChessBoard.Size, ChessBoard.Size];
            QualityBridge.Register(this);
        }

        private void OnDisable() => QualityBridge.Unregister(this);
        public void OnQualityChanged(QualityTier tier) { }

        // ═══════════════════════════════════════════════════════════════
        // BUILD
        // ═══════════════════════════════════════════════════════════════

        private void BuildSquares()
        {
            _squareVisuals = new GameObject[ChessBoard.Size, ChessBoard.Size];

            for (int c = 0; c < ChessBoard.Size; c++)
            {
                for (int r = 0; r < ChessBoard.Size; r++)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = $"Square_{c}_{r}";
                    go.transform.position = SquareWorldPos(c, r);
                    go.transform.localScale = new Vector3(_cellSize * 0.95f, _cellSize * 0.95f, 0.1f);
                    RemoveCollider(go);

                    bool isLight = (c + r) % 2 == 1;
                    SetColor(go, isLight ? LightSquare : DarkSquare, 0f);
                    _squareVisuals[c, r] = go;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // REFRESH
        // ═══════════════════════════════════════════════════════════════

        public void RefreshAll()
        {
            for (int c = 0; c < ChessBoard.Size; c++)
            {
                for (int r = 0; r < ChessBoard.Size; r++)
                {
                    var piece = _board.GetCell(c, r);

                    // Destroy old piece visual
                    if (_pieceVisuals[c, r] != null)
                    {
                        Destroy(_pieceVisuals[c, r]);
                        _pieceVisuals[c, r] = null;
                    }

                    if (piece.IsEmpty) continue;

                    // Create piece visual
                    var go = CreatePieceVisual(piece, c, r);
                    _pieceVisuals[c, r] = go;
                }
            }
        }

        private GameObject CreatePieceVisual(ChessPiece piece, int col, int row)
        {
            PrimitiveType shape;
            Vector3 scale;
            float zOffset;

            GetPieceGeometry(piece.Type, out shape, out scale, out zOffset);

            var go = GameObject.CreatePrimitive(shape);
            go.name = $"Piece_{piece}_{col}_{row}";
            go.transform.position = PieceWorldPos(col, row, zOffset);
            go.transform.localScale = scale * _cellSize;
            RemoveCollider(go);

            Color color = piece.IsWhite ? WhiteColor : BlackColor;
            SetColor(go, color, 0.4f);

            return go;
        }

        private void GetPieceGeometry(PieceType type, out PrimitiveType shape,
                                      out Vector3 scale, out float zOffset)
        {
            // All pieces are distinct by shape/scale for visual differentiation
            switch (type)
            {
                case PieceType.Pawn:
                    shape = PrimitiveType.Cylinder;
                    scale = new Vector3(0.3f, 0.08f, 0.3f);
                    zOffset = -0.08f;
                    break;
                case PieceType.Knight:
                    shape = PrimitiveType.Sphere;
                    scale = new Vector3(0.35f, 0.35f, 0.25f);
                    zOffset = -0.13f;
                    break;
                case PieceType.Bishop:
                    shape = PrimitiveType.Cylinder;
                    scale = new Vector3(0.22f, 0.18f, 0.22f);
                    zOffset = -0.15f;
                    break;
                case PieceType.Rook:
                    shape = PrimitiveType.Cube;
                    scale = new Vector3(0.32f, 0.32f, 0.28f);
                    zOffset = -0.12f;
                    break;
                case PieceType.Queen:
                    shape = PrimitiveType.Sphere;
                    scale = new Vector3(0.38f, 0.50f, 0.38f);
                    zOffset = -0.20f;
                    break;
                case PieceType.King:
                    shape = PrimitiveType.Capsule;
                    scale = new Vector3(0.28f, 0.30f, 0.28f);
                    zOffset = -0.22f;
                    break;
                default:
                    shape = PrimitiveType.Sphere;
                    scale = Vector3.one * 0.2f;
                    zOffset = -0.1f;
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // WORLD POSITIONS
        // ═══════════════════════════════════════════════════════════════

        private Vector3 SquareWorldPos(int col, int row)
        {
            return new Vector3(
                _originX + col * _cellSize,
                _originY + row * _cellSize,
                0f);
        }

        private Vector3 PieceWorldPos(int col, int row, float zOffset)
        {
            return new Vector3(
                _originX + col * _cellSize,
                _originY + row * _cellSize,
                zOffset);
        }

        public Vector3 GetBoardCenter()
        {
            return new Vector3(
                _originX + (ChessBoard.Size - 1) * _cellSize * 0.5f,
                _originY + (ChessBoard.Size - 1) * _cellSize * 0.5f,
                0f);
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        private static void RemoveCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        private static void SetColor(GameObject go, Color color, float emissionStrength)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mr.material.color = color;
                mr.material.SetFloat("_Metallic", 0.2f);
                mr.material.SetFloat("_Smoothness", 0.5f);
                if (emissionStrength > 0.01f)
                {
                    mr.material.EnableKeyword("_EMISSION");
                    mr.material.SetColor("_EmissionColor", color * emissionStrength);
                }
            }
        }
    }
}
