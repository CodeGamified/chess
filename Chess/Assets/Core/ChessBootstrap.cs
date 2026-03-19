// Copyright CodeGamified 2025-2026
// MIT License — Chess
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using CodeGamified.Camera;
using CodeGamified.Time;
using CodeGamified.Settings;
using CodeGamified.Quality;
using CodeGamified.Bootstrap;
using Chess.Game;
using Chess.Scripting;
using Chess.AI;
using Chess.UI;

namespace Chess.Core
{
    /// <summary>
    /// Bootstrap for Chess — code-controlled two-player strategy game.
    ///
    /// Architecture (same pattern as all CodeGamified games):
    ///   - Instantiate managers → wire cross-references → configure scene
    ///   - .engine submodule gives us TUI + Code Execution for free
    ///   - Players don't click pieces — they WRITE CODE to choose moves
    ///   - "Unit test" your chess AI by watching it play at 100x speed
    ///
    /// Coordinate system: XY plane. Board is vertical, centered around origin.
    /// White (Player) at bottom (rows 0-1), Black (AI) at top (rows 6-7).
    /// col 0-7 = a-h, row 0-7 = ranks 1-8.
    /// Attach to a GameObject. Press Play → Chess appears.
    /// </summary>
    public class ChessBootstrap : GameBootstrap, IQualityResponsive
    {
        protected override string LogTag => "CHESS";

        // =================================================================
        // INSPECTOR
        // =================================================================

        [Header("Board")]
        [Tooltip("Size of each cell in world units")]
        public float cellSize = 1.0f;

        [Header("AI")]
        [Tooltip("AI search depth (plies). 2=easy, 3=medium, 4=hard")]
        public int aiDepth = 3;

        [Header("Match")]
        [Tooltip("Auto-restart after game over")]
        public bool autoRestart = true;

        [Tooltip("Delay before auto-restart (sim-seconds)")]
        public float restartDelay = 3f;

        [Header("Time")]
        [Tooltip("Enable time scale modulation for fast testing")]
        public bool enableTimeScale = true;

        [Header("Scripting")]
        [Tooltip("Enable code execution (.engine)")]
        public bool enableScripting = true;

        [Header("Camera")]
        public bool configureCamera = true;

        // =================================================================
        // RUNTIME REFERENCES
        // =================================================================

        private ChessBoard _board;
        private ChessMatchManager _match;
        private ChessRenderer _renderer;
        private ChessProgram _playerProgram;
        private ChessAIController _aiController;
        private ChessTUIManager _tuiManager;

        // Camera
        private CameraAmbientMotion _cameraSway;

        // Post-processing
        private Bloom _bloom;
        private Volume _postProcessVolume;

        // =================================================================
        // UPDATE
        // =================================================================

        private void Update()
        {
            UpdateBloomScale();
        }

        private void UpdateBloomScale()
        {
            if (_bloom == null || !_bloom.active) return;
            var cam = Camera.main;
            if (cam == null) return;
            var center = _renderer != null ? _renderer.GetBoardCenter() : Vector3.zero;
            float dist = Vector3.Distance(cam.transform.position, center);
            float defaultDist = 10f;
            float scale = Mathf.Clamp01(defaultDist / Mathf.Max(dist, 0.01f));
            _bloom.intensity.value = Mathf.Lerp(0.5f, 1.0f, scale);
        }

        // =================================================================
        // BOOTSTRAP
        // =================================================================

        private void Start()
        {
            Log("♔ Chess Bootstrap starting...");

            SettingsBridge.Load();
            QualityBridge.SetTier((QualityTier)SettingsBridge.QualityLevel);
            QualityBridge.Register(this);
            Log($"Settings loaded (Quality={SettingsBridge.QualityLevel}, Font={SettingsBridge.FontSize}pt)");

            SetupSimulationTime();
            SetupCamera();
            CreateBoard();
            CreateMatchManager();
            CreateRenderer();
            CreateInputProvider();

            if (enableScripting) CreatePlayerProgram();

            CreateAIController();
            CreateTUIManager();

            WireEvents();
            StartCoroutine(RunBootSequence());
        }

        public void OnQualityChanged(QualityTier tier)
        {
            Log($"Quality changed → {tier}");
        }

        // =================================================================
        // SIMULATION TIME
        // =================================================================

        private void SetupSimulationTime()
        {
            EnsureSimulationTime<ChessSimulationTime>();
        }

        // =================================================================
        // CAMERA — front-facing perspective view of XY board
        // =================================================================

        private Vector3 BoardCenter()
        {
            float boardSize = ChessBoard.Size * cellSize;
            return new Vector3(0f, boardSize * 0.5f, 0f);
        }

        private void SetupCamera()
        {
            if (!configureCamera) return;

            var cam = EnsureCamera();

            cam.orthographic = false;
            cam.fieldOfView = 60f;
            var center = BoardCenter();
            float boardSize = ChessBoard.Size * cellSize;
            float camZ = -boardSize * 1.1f;
            cam.transform.position = new Vector3(center.x, center.y, camZ);
            cam.transform.LookAt(center, Vector3.up);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.01f, 0.02f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;

            // Ambient sway
            _cameraSway = cam.gameObject.AddComponent<CameraAmbientMotion>();
            _cameraSway.lookAtTarget = center;

            // Post-processing: bloom
            var camData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (camData == null)
                camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;

            var volumeGO = new GameObject("PostProcessVolume");
            _postProcessVolume = volumeGO.AddComponent<Volume>();
            _postProcessVolume.isGlobal = true;
            _postProcessVolume.priority = 1;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _bloom = profile.Add<Bloom>();
            _bloom.threshold.overrideState = true;
            _bloom.threshold.value = 0.8f;
            _bloom.intensity.overrideState = true;
            _bloom.intensity.value = 1.0f;
            _bloom.scatter.overrideState = true;
            _bloom.scatter.value = 0.5f;
            _bloom.clamp.overrideState = true;
            _bloom.clamp.value = 20f;
            _bloom.highQualityFiltering.overrideState = true;
            _bloom.highQualityFiltering.value = true;
            _postProcessVolume.profile = profile;

            Log($"Camera: perspective, FOV=60, Z={camZ:F1} + sway + bloom");
        }

        // =================================================================
        // DOMAIN OBJECTS
        // =================================================================

        private void CreateBoard()
        {
            _board = new ChessBoard();
            _board.Initialize();
            Log($"Created ChessBoard (8×8, {_board.WhitePieces} vs {_board.BlackPieces} pieces)");
        }

        // =================================================================
        // MATCH MANAGER
        // =================================================================

        private void CreateMatchManager()
        {
            var go = new GameObject("MatchManager");
            _match = go.AddComponent<ChessMatchManager>();
            _match.Initialize(_board, autoRestart, restartDelay, aiDepth);
            Log($"Created MatchManager (AI depth={aiDepth}, autoRestart={autoRestart})");
        }

        // =================================================================
        // RENDERER
        // =================================================================

        private void CreateRenderer()
        {
            var go = new GameObject("ChessRenderer");
            _renderer = go.AddComponent<ChessRenderer>();
            _renderer.Initialize(_board, cellSize);
            Log($"Created ChessRenderer (cellSize={cellSize})");
        }

        // =================================================================
        // INPUT PROVIDER
        // =================================================================

        private void CreateInputProvider()
        {
            var go = new GameObject("InputProvider");
            go.AddComponent<ChessInputProvider>();
            Log("Created ChessInputProvider (0-9 keys for move selection)");
        }

        // =================================================================
        // PLAYER SCRIPTING (.engine powered)
        // =================================================================

        private void CreatePlayerProgram()
        {
            var go = new GameObject("PlayerProgram");
            _playerProgram = go.AddComponent<ChessProgram>();
            _playerProgram.Initialize(_match);
            Log("Created PlayerProgram (code-controlled Chess AI)");
        }

        // =================================================================
        // AI CONTROLLER (script-driven Black)
        // =================================================================

        private void CreateAIController()
        {
            var go = new GameObject("AIController");
            _aiController = go.AddComponent<ChessAIController>();
            _aiController.Initialize(_match, AIDifficulty.Medium);
            _match.UseScriptAI = true;
            Log($"Created AIController (difficulty={_aiController.Difficulty}, script AI active)");
        }

        // =================================================================
        // TUI MANAGER
        // =================================================================

        private void CreateTUIManager()
        {
            var go = new GameObject("TUIManager");
            _tuiManager = go.AddComponent<ChessTUIManager>();
            _tuiManager.Initialize(_match, _playerProgram, _aiController);
            Log("Created TUIManager (left debugger + right debugger + status panel)");
        }

        // =================================================================
        // EVENT WIRING
        // =================================================================

        private void WireEvents()
        {
            if (SimulationTime.Instance != null)
            {
                SimulationTime.Instance.OnTimeScaleChanged += s => Log($"Time scale → {s:F0}x");
                SimulationTime.Instance.OnPausedChanged += p => Log(p ? "⏸ PAUSED" : "▶ RESUMED");
            }

            if (_match != null)
            {
                _match.OnMatchStarted += () =>
                {
                    Log("MATCH STARTED");
                    _renderer?.RefreshAll();
                };

                _match.OnMoveMade += move =>
                {
                    Log($"MOVE │ {move}");
                    _renderer?.RefreshAll();
                };

                _match.OnCheck += () =>
                    Log($"CHECK! │ {(_board.SideToMove == PieceColor.White ? "White" : "Black")} in check");

                _match.OnWin += winner =>
                {
                    string who = winner == 1 ? "WHITE (Player)" : "BLACK (AI)";
                    string how = _board.IsCheckmate() ? "CHECKMATE" : "RESIGNATION";
                    Log($"{how} │ {who} wins │ P:{_match.PlayerWins} AI:{_match.AIWins} D:{_match.Draws}");
                };

                _match.OnDraw += () =>
                {
                    string reason = "DRAW";
                    if (_board.IsStalemate()) reason = "STALEMATE";
                    else if (_board.IsThreefoldRepetition) reason = "THREEFOLD REPETITION";
                    else if (_board.IsFiftyMoveRule) reason = "50-MOVE RULE";
                    else if (_board.IsInsufficientMaterial()) reason = "INSUFFICIENT MATERIAL";
                    Log($"{reason} │ P:{_match.PlayerWins} AI:{_match.AIWins} D:{_match.Draws}");
                };

                _match.OnGameOver += () =>
                    Log($"GAME OVER │ Matches: {_match.MatchesPlayed} │ Moves: {_board.TotalMoves}");

                _match.OnBoardChanged += () =>
                    _renderer?.RefreshAll();
            }
        }

        // =================================================================
        // BOOT SEQUENCE
        // =================================================================

        private IEnumerator RunBootSequence()
        {
            yield return null;
            yield return null;

            LogDivider();
            Log("♔ CHESS — The Ultimate Strategy");
            LogDivider();
            LogStatus("BOARD", "8×8 standard chess");
            LogStatus("AI DEPTH", $"{aiDepth} plies (negamax + alpha-beta)");
            LogStatus("CELL SIZE", $"{cellSize}");
            LogEnabled("SCRIPTING", enableScripting);
            LogEnabled("TIME SCALE", enableTimeScale);
            LogEnabled("AUTO RESTART", autoRestart);
            LogDivider();
            Log("Features: castling, en passant, promotion, check/checkmate/stalemate");
            Log("Piece values: P=100 N=320 B=330 R=500 Q=900 K=20000");
            LogDivider();

            _match.StartMatch();
            Log("First match started — e4!");
        }

        private void OnDestroy()
        {
            QualityBridge.Unregister(this);
        }
    }
}
