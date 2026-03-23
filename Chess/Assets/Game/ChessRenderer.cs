// Copyright CodeGamified 2025-2026
// MIT License — Chess
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CodeGamified.Quality;

namespace Chess.Game
{
    /// <summary>
    /// Visual renderer for the Chess board.
    /// 8×8 on XY plane: col→X, row→Y.
    /// Light/dark alternating squares.
    /// Pieces loaded from OBJ meshes (Resources/OBJ/) with neon emissive glow
    /// and 75% alpha transparency:
    ///   - White pieces: bright cream/gold neon
    ///   - Black pieces: dark charcoal/purple neon
    /// </summary>
    public class ChessRenderer : MonoBehaviour, IQualityResponsive
    {
        private ChessBoard _board;
        private float _cellSize;

        private GameObject[,] _squareVisuals;
        private GameObject[,] _pieceVisuals;
        private float _originX;
        private float _originY;

        // Animation
        private const float MOVE_DURATION = 0.25f;
        private const float ARC_HEIGHT = 0.4f;
        private const float CAPTURE_DURATION = 0.3f;
        private const float PROMO_FLASH_DURATION = 0.4f;
        private static readonly Color CaptureFlashColor = new Color(1f, 0.1f, 0.1f);
        private static readonly Color CheckFlashColor = new Color(1f, 0.15f, 0.05f);
        private Coroutine _activeAnim;
        public bool IsAnimating => _activeAnim != null;

        // Last-move tracking for glow highlight
        private int _lastMoveCol = -1;
        private int _lastMoveRow = -1;
        private const float EMISSION_DIM = 1.25f;
        private const float EMISSION_BRIGHT = 2.5f;
        private const float PIECE_ALPHA = 0.90f;

        // OBJ mesh cache (loaded once from Resources/OBJ/)
        private static readonly Dictionary<PieceType, Mesh> _meshCache = new Dictionary<PieceType, Mesh>();
        private static bool _meshesLoaded;

        // Square colors — subtle grays, let the pieces pop
        private static readonly Color LightSquare = new Color(0.45f, 0.45f, 0.48f);  // cool light gray
        private static readonly Color DarkSquare  = new Color(0.22f, 0.22f, 0.25f);  // dark charcoal

        // Piece colors — hot neon glow
        private static readonly Color WhiteColor = new Color(0.0f, 1.0f, 0.95f);   // electric cyan
        private static readonly Color BlackColor = new Color(1.0f, 0.15f, 0.60f);  // hot pink

        // Last-move highlight — neon green
        private static readonly Color HighlightColor = new Color(0.2f, 1.0f, 0.4f, 0.5f);

        /// <summary>Cell size in world units (public for trail rendering).</summary>
        public float CellSize => _cellSize;

        /// <summary>World-space center of a board square.</summary>
        public Vector3 SquareCenter(int col, int row)
        {
            return SquareWorldPos(col, row);
        }

        public void Initialize(ChessBoard board, float cellSize = 1.0f)
        {
            _board = board;
            _cellSize = cellSize;

            _originX = -(ChessBoard.Size * cellSize) * 0.5f + cellSize * 0.5f;
            _originY = cellSize * 0.5f;

            BuildSquares();
            _pieceVisuals = new GameObject[ChessBoard.Size, ChessBoard.Size];
            QualityBridge.Register(this);
        }

        private void OnDisable() => QualityBridge.Unregister(this);
        public void OnQualityChanged(QualityTier tier) { }

        private void Update()
        {
            PulseCheckedKing();
        }

        /// <summary>
        /// Each frame, if a king is in check, pulse its emission between
        /// its normal color and red. Resets non-checked kings to normal.
        /// </summary>
        private void PulseCheckedKing()
        {
            if (_board == null || _pieceVisuals == null) return;

            PieceColor sideInCheck = PieceColor.None;
            if (_board.IsInCheck(PieceColor.White)) sideInCheck = PieceColor.White;
            else if (_board.IsInCheck(PieceColor.Black)) sideInCheck = PieceColor.Black;

            // Pulse the checked king
            if (sideInCheck != PieceColor.None)
            {
                _board.GetKingPos(sideInCheck, out int kc, out int kr);
                var kingGO = _pieceVisuals[kc, kr];
                if (kingGO != null)
                {
                    var mr = kingGO.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        float pulse = Mathf.Abs(Mathf.Sin(Time.time * 5f));
                        Color baseCol = sideInCheck == PieceColor.White ? WhiteColor : BlackColor;
                        Color emitCol = Color.Lerp(baseCol, CheckFlashColor, pulse);
                        float intensity = Mathf.Lerp(EMISSION_BRIGHT, 5f, pulse);
                        mr.material.EnableKeyword("_EMISSION");
                        mr.material.SetColor("_EmissionColor", emitCol * intensity);
                    }
                }
            }
        }

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
                    Color sqColor = isLight ? LightSquare : DarkSquare;
                    float sqEmission = isLight ? 0.15f : 0.08f;
                    SetColor(go, sqColor, sqEmission, 0.50f);
                    _squareVisuals[c, r] = go;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // REFRESH
        // ═══════════════════════════════════════════════════════════════

        public void RefreshAll()
        {
            // Cancel any in-flight animation so we don't fight it
            if (_activeAnim != null)
            {
                StopCoroutine(_activeAnim);
                _activeAnim = null;
            }

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

                    // Bright glow on last-moved piece, dim on others
                    if (c == _lastMoveCol && r == _lastMoveRow)
                        SetEmission(go, piece.IsWhite ? WhiteColor : BlackColor, EMISSION_BRIGHT);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // ANIMATED MOVE
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Animate a piece sliding from its source square to its destination,
        /// with a small arc. Captured pieces flash red and shrink to nothing.
        /// Promotions flash the new piece. Handles castling rook slides.
        /// </summary>
        public void AnimateMove(ChessMove move)
        {
            // If already animating, snap the previous one and start fresh
            if (_activeAnim != null)
            {
                StopCoroutine(_activeAnim);
                _activeAnim = null;
                RefreshAll();
            }

            var movingPiece = _pieceVisuals[move.FromCol, move.FromRow];
            if (movingPiece == null)
            {
                RefreshAll();
                return;
            }

            // Track destination for glow highlight after RefreshAll
            _lastMoveCol = move.ToCol;
            _lastMoveRow = move.ToRow;

            // Boost the moving piece to full brightness immediately
            var movingBoardPiece = _board.GetCell(move.ToCol, move.ToRow);
            if (!movingBoardPiece.IsEmpty)
                SetEmission(movingPiece, movingBoardPiece.IsWhite ? WhiteColor : BlackColor, EMISSION_BRIGHT);

            // Collect captured piece for death animation (don't destroy yet)
            GameObject capturedPiece = null;

            // Normal capture at destination
            if (_pieceVisuals[move.ToCol, move.ToRow] != null)
            {
                capturedPiece = _pieceVisuals[move.ToCol, move.ToRow];
                _pieceVisuals[move.ToCol, move.ToRow] = null;
            }

            // En passant: the captured pawn is on the mover's row
            if (move.IsEnPassant)
            {
                int epPawnRow = move.FromRow;
                if (_pieceVisuals[move.ToCol, epPawnRow] != null)
                {
                    capturedPiece = _pieceVisuals[move.ToCol, epPawnRow];
                    _pieceVisuals[move.ToCol, epPawnRow] = null;
                }
            }

            // Start capture death animation in parallel
            if (capturedPiece != null)
                StartCoroutine(CaptureDeathCoroutine(capturedPiece));

            // Update the array: move piece reference
            _pieceVisuals[move.FromCol, move.FromRow] = null;
            _pieceVisuals[move.ToCol, move.ToRow] = movingPiece;

            // Castling: also slide the rook
            GameObject castlingRook = null;
            Vector3 rookTarget = Vector3.zero;
            if (move.IsCastleK || move.IsCastleQ)
            {
                int rookFromCol = move.IsCastleK ? 7 : 0;
                int rookToCol   = move.IsCastleK ? 5 : 3;
                int rookRow = move.FromRow;

                castlingRook = _pieceVisuals[rookFromCol, rookRow];
                if (castlingRook != null)
                {
                    _pieceVisuals[rookFromCol, rookRow] = null;
                    _pieceVisuals[rookToCol, rookRow] = castlingRook;
                    rookTarget = PieceWorldPos(rookToCol, rookRow, -0.15f);
                }
            }

            var target = PieceWorldPos(move.ToCol, move.ToRow, -0.15f);
            _activeAnim = StartCoroutine(SlideCoroutine(
                movingPiece, target, castlingRook, rookTarget, move));
        }

        private IEnumerator SlideCoroutine(GameObject piece, Vector3 target,
            GameObject castlingRook, Vector3 rookTarget, ChessMove move)
        {
            Vector3 start = piece.transform.position;
            Vector3 rookStart = castlingRook != null ? castlingRook.transform.position : Vector3.zero;
            float elapsed = 0f;

            while (elapsed < MOVE_DURATION)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / MOVE_DURATION);
                float smooth = t * t * (3f - 2f * t);

                float arc = 4f * ARC_HEIGHT * smooth * (1f - smooth);
                Vector3 pos = Vector3.Lerp(start, target, smooth);
                pos.z -= arc;
                piece.transform.position = pos;

                if (castlingRook != null)
                    castlingRook.transform.position = Vector3.Lerp(rookStart, rookTarget, smooth);

                yield return null;
            }

            piece.transform.position = target;
            if (castlingRook != null)
                castlingRook.transform.position = rookTarget;

            // Promotion: flash the new piece
            if (move.IsPromotion)
            {
                yield return PromotionFlashCoroutine(move.ToCol, move.ToRow);
            }

            _activeAnim = null;
            RefreshAll();
        }

        /// <summary>
        /// Captured piece flashes red, then scales down to 0 and is destroyed.
        /// </summary>
        private IEnumerator CaptureDeathCoroutine(GameObject victim)
        {
            if (victim == null) yield break;

            var mr = victim.GetComponent<MeshRenderer>();
            Vector3 originalScale = victim.transform.localScale;
            float elapsed = 0f;

            while (elapsed < CAPTURE_DURATION)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / CAPTURE_DURATION);

                // Flash red emission with pulsing intensity
                if (mr != null)
                {
                    float pulse = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 3f));
                    float intensity = Mathf.Lerp(3f, 6f, pulse);
                    mr.material.EnableKeyword("_EMISSION");
                    mr.material.SetColor("_EmissionColor", CaptureFlashColor * intensity);
                    mr.material.color = new Color(
                        Mathf.Lerp(mr.material.color.r, CaptureFlashColor.r, t),
                        Mathf.Lerp(mr.material.color.g, CaptureFlashColor.g, t),
                        Mathf.Lerp(mr.material.color.b, CaptureFlashColor.b, t),
                        mr.material.color.a);
                }

                // Scale down with ease-in (accelerate into nothing)
                float shrink = 1f - (t * t);
                victim.transform.localScale = originalScale * shrink;

                yield return null;
            }

            Destroy(victim);
        }

        /// <summary>
        /// After a pawn promotes, destroy the pawn visual and spawn the new piece
        /// with a scale-up flash effect.
        /// </summary>
        private IEnumerator PromotionFlashCoroutine(int col, int row)
        {
            // Destroy the old pawn visual at the promotion square
            if (_pieceVisuals[col, row] != null)
            {
                Destroy(_pieceVisuals[col, row]);
                _pieceVisuals[col, row] = null;
            }

            // Create the promoted piece (board already has the new piece type)
            var piece = _board.GetCell(col, row);
            if (piece.IsEmpty) yield break;

            var go = CreatePieceVisual(piece, col, row);
            _pieceVisuals[col, row] = go;

            var mr = go.GetComponent<MeshRenderer>();
            Vector3 fullScale = go.transform.localScale;
            Color baseColor = piece.IsWhite ? WhiteColor : BlackColor;

            float elapsed = 0f;
            while (elapsed < PROMO_FLASH_DURATION)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / PROMO_FLASH_DURATION);

                // Scale up from 0 with overshoot bounce
                float scale;
                if (t < 0.6f)
                    scale = (t / 0.6f) * 1.3f;  // overshoot to 130%
                else
                    scale = Mathf.Lerp(1.3f, 1f, (t - 0.6f) / 0.4f);  // settle to 100%
                go.transform.localScale = fullScale * scale;

                // Bright white flash that fades to normal color
                if (mr != null)
                {
                    float flash = 1f - t;
                    float intensity = Mathf.Lerp(2.5f, 8f, flash);
                    Color emitColor = Color.Lerp(baseColor, Color.white, flash * 0.7f);
                    mr.material.EnableKeyword("_EMISSION");
                    mr.material.SetColor("_EmissionColor", emitColor * intensity);
                }

                yield return null;
            }

            go.transform.localScale = fullScale;
            if (mr != null)
            {
                mr.material.EnableKeyword("_EMISSION");
                mr.material.SetColor("_EmissionColor", baseColor * EMISSION_BRIGHT);
            }
        }

        private GameObject CreatePieceVisual(ChessPiece piece, int col, int row)
        {
            LoadMeshes();

            float zOffset = -0.15f;
            Mesh mesh;

            if (_meshCache.TryGetValue(piece.Type, out mesh) && mesh != null)
            {
                var go = new GameObject($"Piece_{piece}_{col}_{row}");
                go.AddComponent<MeshFilter>().mesh = mesh;
                go.AddComponent<MeshRenderer>();
                go.transform.position = PieceWorldPos(col, row, zOffset);
                Quaternion rot;
                if (piece.Type == PieceType.Knight)
                    rot = piece.IsWhite
                        ? Quaternion.Euler(-180f, 0f, 0f)
                        : Quaternion.Euler(-180f, 0f, -180f);
                else
                    rot = Quaternion.Euler(-180f, 0f, 0f);
                go.transform.rotation = rot;
                go.transform.localScale = Vector3.one * GetPieceScale(piece.Type) * _cellSize;

                Color color = piece.IsWhite ? WhiteColor : BlackColor;
                SetColor(go, color, EMISSION_DIM, PIECE_ALPHA);
                return go;
            }

            // Fallback: procedural sphere if OBJ not loaded
            var fallback = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fallback.name = $"Piece_{piece}_{col}_{row}";
            fallback.transform.position = PieceWorldPos(col, row, zOffset);
            fallback.transform.localScale = Vector3.one * _cellSize * 0.3f;
            RemoveCollider(fallback);

            Color fc = piece.IsWhite ? WhiteColor : BlackColor;
            SetColor(fallback, fc, EMISSION_DIM, PIECE_ALPHA);
            return fallback;
        }

        private static void LoadMeshes()
        {
            if (_meshesLoaded) return;
            _meshesLoaded = true;

            var pieces = new[] {
                (PieceType.Pawn,   "Pawn"),
                (PieceType.Knight, "Knight"),
                (PieceType.Bishop, "Bishop"),
                (PieceType.Rook,   "Rook"),
                (PieceType.Queen,  "Queen"),
                (PieceType.King,   "King"),
            };

            foreach (var (type, name) in pieces)
            {
                var prefab = Resources.Load<GameObject>($"OBJ/{name}");
                if (prefab == null) continue;
                var mf = prefab.GetComponentInChildren<MeshFilter>();
                if (mf != null)
                    _meshCache[type] = mf.sharedMesh;
            }
        }

        private static float GetPieceScale(PieceType type)
        {
            switch (type)
            {
                case PieceType.Pawn:   return 0.014f;
                case PieceType.Knight: return 0.015f;
                case PieceType.Bishop: return 0.016f;
                case PieceType.Rook:   return 0.015f;
                case PieceType.Queen:  return 0.0175f;
                case PieceType.King:   return 0.019f;
                default:               return 0.0125f;
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

        private static void SetColor(GameObject go, Color color, float emissionStrength, float alpha = 1f)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) return;

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));

            if (alpha < 1f)
            {
                mat.SetFloat("_Surface", 1); // Transparent
                mat.SetFloat("_Blend", 0);   // Alpha blend
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            mat.color = new Color(color.r, color.g, color.b, alpha);
            mat.SetFloat("_Metallic", 0.1f);
            mat.SetFloat("_Smoothness", 0.7f);

            if (emissionStrength > 0.01f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * emissionStrength);
            }

            mr.material = mat;
        }

        private static void SetEmission(GameObject go, Color color, float strength)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null || mr.material == null) return;
            mr.material.EnableKeyword("_EMISSION");
            mr.material.SetColor("_EmissionColor", color * strength);
        }
    }
}
