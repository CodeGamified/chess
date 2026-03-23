// Copyright CodeGamified 2025-2026
// MIT License — Chess
using System.Collections;
using UnityEngine;

namespace Chess.Game
{
    /// <summary>
    /// Last-move trail — two LineRenderers (one per player) draw from→to arcs
    /// showing each side's most recent move.
    /// HDR emissive lines that bloom picks up (same aesthetic as TetrisBlockTrail).
    /// </summary>
    public class ChessMoveTrail : MonoBehaviour
    {
        private ChessRenderer _renderer;

        private LineRenderer _whiteLine;
        private LineRenderer _blackLine;
        private Material _lineMaterialProto;

        private Coroutine _whiteFade;
        private Coroutine _blackFade;

        // Aesthetic constants — matched to TetrisBlockTrail
        private const float LINE_START_WIDTH = 0.10f;
        private const float LINE_END_WIDTH   = 0.03f;
        private const float HDR_MULTIPLIER   = 2.5f;
        private const float FADE_DURATION    = 0.3f;
        private const float LINE_Z           = -0.08f; // between squares (z=0) and pieces (z=-0.15)

        // Colors — match ChessRenderer piece colors
        private static readonly Color WhiteColor = new Color(0.0f, 1.0f, 0.95f);  // electric cyan
        private static readonly Color BlackColor = new Color(1.0f, 0.15f, 0.60f);  // hot pink

        public void Initialize(ChessRenderer renderer)
        {
            _renderer = renderer;
            BuildMaterial();
            _whiteLine = CreateLine("WhiteMoveTrail");
            _blackLine = CreateLine("BlackMoveTrail");
        }

        private void BuildMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Universal Render Pipeline/Unlit");
            _lineMaterialProto = new Material(shader);
            _lineMaterialProto.SetFloat("_Surface", 0);
            _lineMaterialProto.SetColor("_BaseColor", Color.white);
        }

        private LineRenderer CreateLine(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);

            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 0;
            lr.startWidth = LINE_START_WIDTH;
            lr.endWidth = LINE_END_WIDTH;
            lr.useWorldSpace = true;
            lr.numCornerVertices = 3;
            lr.numCapVertices = 3;
            lr.material = new Material(_lineMaterialProto);

            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.25f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            lr.colorGradient = grad;

            return lr;
        }

        /// <summary>Show a move trail for the given side.</summary>
        public void ShowMove(ChessMove move, bool isWhite)
        {
            var line = isWhite ? _whiteLine : _blackLine;
            ref Coroutine fade = ref (isWhite ? ref _whiteFade : ref _blackFade);

            // Cancel any in-flight fade
            if (fade != null)
            {
                StopCoroutine(fade);
                fade = null;
            }

            Color baseColor = isWhite ? WhiteColor : BlackColor;
            Color hdr = baseColor * HDR_MULTIPLIER;

            var mat = line.material;
            mat.SetColor("_BaseColor", hdr);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", hdr);
            }

            Vector3 from = SquareWorld(move.FromCol, move.FromRow);
            Vector3 to   = SquareWorld(move.ToCol, move.ToRow);

            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
        }

        /// <summary>Fade out the previous trail for a side before showing the new one.</summary>
        public void TransitionMove(ChessMove move, bool isWhite)
        {
            var line = isWhite ? _whiteLine : _blackLine;
            ref Coroutine fade = ref (isWhite ? ref _whiteFade : ref _blackFade);

            // If there's already a visible trail, fade it then show new
            if (line.positionCount > 0)
            {
                if (fade != null) StopCoroutine(fade);
                fade = StartCoroutine(FadeThenShow(line, move, isWhite));
            }
            else
            {
                ShowMove(move, isWhite);
            }
        }

        public void ClearAll()
        {
            if (_whiteFade != null) { StopCoroutine(_whiteFade); _whiteFade = null; }
            if (_blackFade != null) { StopCoroutine(_blackFade); _blackFade = null; }
            ClearLine(_whiteLine);
            ClearLine(_blackLine);
        }

        private void ClearLine(LineRenderer lr)
        {
            if (lr != null) lr.positionCount = 0;
        }

        private IEnumerator FadeThenShow(LineRenderer lr, ChessMove newMove, bool isWhite)
        {
            // Fade out old trail
            Color originalBase = lr.material.HasProperty("_BaseColor")
                ? lr.material.GetColor("_BaseColor")
                : Color.white;

            float elapsed = 0f;
            while (elapsed < FADE_DURATION)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / FADE_DURATION;
                Color faded = Color.Lerp(originalBase, Color.black, t);
                var mat = lr.material;
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", faded);
                if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", faded);
                yield return null;
            }

            ClearLine(lr);

            // Show the new move immediately
            ShowMove(newMove, isWhite);

            if (isWhite) _whiteFade = null;
            else _blackFade = null;
        }

        private Vector3 SquareWorld(int col, int row)
        {
            Vector3 center = _renderer.SquareCenter(col, row);
            center.z = LINE_Z;
            return center;
        }
    }
}
