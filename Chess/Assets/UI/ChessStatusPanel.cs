// Copyright CodeGamified 2025-2026
// MIT License — Chess
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using CodeGamified.Audio;
using CodeGamified.TUI;
using CodeGamified.Time;
using CodeGamified.Settings;
using CodeGamified.Quality;
using UnityEngine.SceneManagement;
using Chess.Game;
using Chess.AI;
using Chess.Core;
using Chess.Scripting;

namespace Chess.UI
{
    /// <summary>
    /// Unified status panel — 7 columns with draggable dividers:
    ///   YOU │ SETTINGS │ MATCH │ CHESS │ CONTROLS │ AUDIO │ OPPONENT
    /// Same pattern as Pong/Checkers StatusPanel.
    /// </summary>
    public class ChessStatusPanel : TerminalWindow
    {
        // ── Dependencies ────────────────────────────────────────
        private ChessMatchManager _match;
        private ChessProgram _playerProgram;
        private ChessAIController _ai;
        private AIDifficulty? _playerScriptTier;
        private Equalizer _equalizer;

        // ── Column layout (7 columns, 6 draggers) ───────────────
        private const int COL_COUNT = 7;
        private float[] _colRatios = { 0f, 0.11f, 0.22f, 0.33f, 0.67f, 0.78f, 0.89f };
        private int[] _colPositions;
        private TUIColumnDragger[] _colDraggers;
        private bool _columnsReady;

        // ── Overlay bindings ────────────────────────────────────
        private TUIOverlayBinding _overlays;
        private ChessBootstrap _bootstrap;

        // ── ASCII art animation ─────────────────────────────────
        private float _asciiTimer;
        private int _asciiPhase;
        private float[] _revealThresholds;
        private const float AsciiHold = 5f;
        private const float AsciiAnim = 1f;
        private const int AsciiWordCount = 3;
        private const int MaxStatusRows = 10;
        private static readonly char[] GlitchGlyphs =
            "░▒▓█▀▄▌▐╬╫╪╩╦╠╣─│┌┐└┘├┤┬┴┼".ToCharArray();

        private static readonly string[][] AsciiWords =
        {
            new[] // CODE
            {
                "   █████████  ████████  █████████   █████████  ",
                "  ██         ██      ██ ██      ██ ██          ",
                "  ██         ██      ██ ██      ██ ██████████  ",
                "  ██         ██      ██ ██      ██ ██          ",
                "   █████████  ████████  █████████   █████████  ",
            },
            new[] // GAME
            {
                "   █████████  ████████   ████████   █████████  ",
                "  ██         ██      ██ ██  ██  ██ ██          ",
                "  ██   █████ ██████████ ██  ██  ██ ██████████  ",
                "  ██      ██ ██      ██ ██  ██  ██ ██          ",
                "   █████████ ██      ██ ██  ██  ██  █████████  ",
            },
            new[] // CHESS
            {
                "  ███████ ██    ██ ████████ ████████ ████████  ",
                "  ██      ██    ██ ██       ██       ██        ",
                "  ██      ████████ ████████ ████████ ████████  ",
                "  ██      ██    ██ ██             ██       ██  ",
                "  ███████ ██    ██ ████████ ████████ ████████  ",
            },
        };

        private bool IsExpanded => totalRows > 1;

        // ═══════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════════════════

        protected override void Awake()
        {
            base.Awake();
            windowTitle = "CHESS";
            totalRows = MaxStatusRows;
        }

        public void Bind(ChessMatchManager match, ChessProgram playerProgram, ChessAIController ai)
        {
            _match = match;
            _playerProgram = playerProgram;
            _ai = ai;
        }

        public void BindEqualizer(Equalizer equalizer) => _equalizer = equalizer;

        protected override void OnLayoutReady()
        {
            ClampPanelHeight();
            var rt = GetComponent<RectTransform>();
            if (rt == null || rows.Count == 0) return;
            float h = rt.rect.height;
            float rowH = rows[0].RowHeight;
            if (rowH <= 0) return;
            int fitRows = Mathf.Clamp(Mathf.FloorToInt(h / rowH), 2, MaxStatusRows);
            if (fitRows != totalRows)
            {
                for (int i = 0; i < rows.Count; i++)
                    rows[i].gameObject.SetActive(i < fitRows);
                totalRows = fitRows;
            }
            SetupColumns();
        }

        private void ClampPanelHeight()
        {
            if (rows.Count == 0) return;
            float rowH = rows[0].RowHeight;
            if (rowH <= 0) return;
            var rt = GetComponent<RectTransform>();
            if (rt == null || rt.parent == null) return;
            float maxH = MaxStatusRows * rowH;
            float canvasH = ((RectTransform)rt.parent).rect.height;
            if (canvasH <= 0) return;
            float maxAnchorSpan = maxH / canvasH;
            float currentSpan = rt.anchorMax.y - rt.anchorMin.y;
            if (currentSpan > maxAnchorSpan)
            {
                float clampedTop = rt.anchorMin.y + maxAnchorSpan;
                var aMax = rt.anchorMax;
                aMax.y = clampedTop;
                rt.anchorMax = aMax;
                foreach (RectTransform sibling in rt.parent)
                {
                    if (sibling == rt) continue;
                    if (sibling.anchorMin.y < clampedTop && sibling.anchorMax.y > clampedTop)
                    {
                        var sMin = sibling.anchorMin;
                        sMin.y = clampedTop;
                        sibling.anchorMin = sMin;
                    }
                }
            }
        }

        protected override void Update()
        {
            base.Update();
            if (totalRows > MaxStatusRows)
            {
                for (int i = MaxStatusRows; i < rows.Count; i++)
                    rows[i].gameObject.SetActive(false);
                totalRows = MaxStatusRows;
            }
            ClampPanelHeight();
            if (!rowsReady) return;
            _equalizer?.Update(UnityEngine.Time.deltaTime);
            AdvanceAsciiTimer();
            if (IsExpanded) HandleInput();
        }

        // ═══════════════════════════════════════════════════════════════
        // COLUMN LAYOUT
        // ═══════════════════════════════════════════════════════════════

        private void SetupColumns()
        {
            ComputeColumnPositions();
            _hoverColumnPositions = _colPositions;
            foreach (var row in rows)
                row.SetNPanelMode(true, _colPositions);
            _columnsReady = true;
            if (_colDraggers == null)
            {
                _colDraggers = new TUIColumnDragger[COL_COUNT - 1];
                for (int i = 0; i < COL_COUNT - 1; i++)
                {
                    int idx = i;
                    int minPos = (i > 0 ? _colPositions[i] : 0) + 4;
                    int maxPos = (i + 2 < COL_COUNT ? _colPositions[i + 2] : totalChars) - 4;
                    _colDraggers[i] = AddColumnDragger(
                        _colPositions[i + 1], minPos, maxPos, pos => OnColumnDragged(idx, pos));
                }
            }
            else
            {
                float cw = rows.Count > 0 ? rows[0].CharWidth : 10f;
                for (int i = 0; i < COL_COUNT - 1; i++)
                {
                    _colDraggers[i].UpdateCharWidth(cw);
                    _colDraggers[i].UpdatePosition(_colPositions[i + 1]);
                    UpdateDraggerLimits(i);
                }
            }
            BuildAndApplyOverlays();
        }

        private void ComputeColumnPositions()
        {
            _colPositions = new int[COL_COUNT];
            _colPositions[0] = 0;
            for (int i = 1; i < COL_COUNT; i++)
            {
                int minPos = _colPositions[i - 1] + 4;
                int maxPos = totalChars - (COL_COUNT - i) * 4;
                _colPositions[i] = Mathf.Clamp(
                    Mathf.RoundToInt(totalChars * _colRatios[i]), minPos, maxPos);
            }
        }

        private void OnColumnDragged(int draggerIndex, int newPos)
        {
            int colIdx = draggerIndex + 1;
            _colPositions[colIdx] = newPos;
            _colRatios[colIdx] = (float)newPos / totalChars;
            if (draggerIndex > 0) UpdateDraggerLimits(draggerIndex - 1);
            if (draggerIndex < COL_COUNT - 2) UpdateDraggerLimits(draggerIndex + 1);
            ApplyNPanelResize(_colPositions);
            _hoverColumnPositions = _colPositions;
            if (_overlays != null)
                _overlays.Apply(rows, _colPositions, totalChars);
        }

        private void UpdateDraggerLimits(int draggerIdx)
        {
            int minPos = _colPositions[draggerIdx] + 4;
            int maxPos = (draggerIdx + 2 < COL_COUNT ? _colPositions[draggerIdx + 2] : totalChars) - 4;
            _colDraggers[draggerIdx].UpdateLimits(minPos, maxPos);
        }

        private int ColWidth(int colIdx)
        {
            if (_colPositions == null) return 10;
            int end = colIdx + 1 < COL_COUNT ? _colPositions[colIdx + 1] : totalChars;
            return end - _colPositions[colIdx];
        }

        // ═══════════════════════════════════════════════════════════════
        // INPUT
        // ═══════════════════════════════════════════════════════════════

        private void HandleInput()
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (Input.GetKeyDown(KeyCode.F1))
            { if (shift) SetAIDifficulty(AIDifficulty.Easy); else LoadPlayerSample(AIDifficulty.Easy); }
            else if (Input.GetKeyDown(KeyCode.F2))
            { if (shift) SetAIDifficulty(AIDifficulty.Medium); else LoadPlayerSample(AIDifficulty.Medium); }
            else if (Input.GetKeyDown(KeyCode.F3))
            { if (shift) SetAIDifficulty(AIDifficulty.Hard); else LoadPlayerSample(AIDifficulty.Hard); }
            else if (Input.GetKeyDown(KeyCode.F4))
            { if (shift) SetAIDifficulty(AIDifficulty.Expert); else LoadPlayerSample(AIDifficulty.Expert); }

            if (Input.GetKeyDown(KeyCode.R)) ReloadScene();
            if (Input.GetKeyDown(KeyCode.D))
            {
                SimulationTime.Instance?.SetTimeScale(1f);
                SettingsBridge.SetQualityLevel(3); QualityBridge.SetTier(QualityTier.Ultra);
                SettingsBridge.SetFontSize(20f);
                SettingsBridge.SetMasterVolume(0.5f);
                SettingsBridge.SetMusicVolume(0.25f);
                SettingsBridge.SetSfxVolume(0.75f);
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }

            if (Input.GetKeyDown(KeyCode.F5))
                SettingsBridge.SetMasterVolume(SettingsBridge.MasterVolume + (shift ? -0.1f : 0.1f));
            if (Input.GetKeyDown(KeyCode.F6))
                SettingsBridge.SetMusicVolume(SettingsBridge.MusicVolume + (shift ? -0.1f : 0.1f));
            if (Input.GetKeyDown(KeyCode.F7))
                SettingsBridge.SetSfxVolume(SettingsBridge.SfxVolume + (shift ? -0.1f : 0.1f));
        }

        // ═══════════════════════════════════════════════════════════════
        // SCRIPT/AI ACTIONS
        // ═══════════════════════════════════════════════════════════════

        private void LoadPlayerSample(AIDifficulty diff)
        {
            if (_playerProgram == null) return;
            _playerProgram.UploadCode(ChessAIController.GetSampleCode(diff));
            _playerScriptTier = diff;
        }

        private void SetAIDifficulty(AIDifficulty diff)
        {
            if (_ai == null) return;
            _ai.SetDifficulty(diff);
        }

        // ═══════════════════════════════════════════════════════════════
        // OVERLAYS
        // ═══════════════════════════════════════════════════════════════

        static readonly Func<int, int, (int, int)> FullBtnLayout =
            (cs, cw) => (cs + 2, Mathf.Max(4, cw - 2));

        private void BuildAndApplyOverlays()
        {
            if (_overlays == null)
            {
                _overlays = new TUIOverlayBinding();
                if (_bootstrap == null)
                    _bootstrap = FindFirstObjectByType<ChessBootstrap>();

                // Audio sliders (col 5)
                _overlays.Slider(1, 5, () => SettingsBridge.MasterVolume, v => SettingsBridge.SetMasterVolume(v));
                _overlays.Slider(2, 5, () => SettingsBridge.MusicVolume, v => SettingsBridge.SetMusicVolume(v));
                _overlays.Slider(3, 5, () => SettingsBridge.SfxVolume, v => SettingsBridge.SetSfxVolume(v));

                // Controls slider (col 4)
                _overlays.Slider(1, 4,
                    () => SpeedToSlider(SimulationTime.Instance != null ? SimulationTime.Instance.timeScale : 1f),
                    v => SimulationTime.Instance?.SetTimeScale(SliderToSpeed(v)));

                // Quality / Font (col 1)
                _overlays.Slider(1, 1,
                    () => SettingsBridge.QualityLevel / 3f,
                    v => { int lv = Mathf.RoundToInt(v * 3f); SettingsBridge.SetQualityLevel(lv); QualityBridge.SetTier((QualityTier)lv); },
                    step: 1f / 3f);
                _overlays.Slider(2, 1,
                    () => FontToSlider(SettingsBridge.FontSize),
                    v => SettingsBridge.SetFontSize(SliderToFont(v)),
                    step: 1f / 40f);
                if (_bootstrap != null)
                {
                    _overlays.Slider(5, 1,
                        () => (_bootstrap.cellSize - 0.5f) / 1.5f,
                        v => { if (_bootstrap != null) _bootstrap.cellSize = 0.5f + v * 1.5f; },
                        step: 0.1f / 1.5f);
                    _overlays.Slider(6, 1,
                        () => (_bootstrap.aiDepth - 1f) / 4f,
                        v => { if (_bootstrap != null) _bootstrap.aiDepth = Mathf.RoundToInt(1f + v * 4f); },
                        step: 1f / 4f);
                }

                RegisterButtonOverlays();
            }
            _overlays.Apply(rows, _colPositions, totalChars);
        }

        private void RegisterButtonOverlays()
        {
            _overlays.Button(2, 4, FullBtnLayout, _ => SimulationTime.Instance?.TogglePause());

            _overlays.Button(4, 0, FullBtnLayout, _ => LoadPlayerSample(AIDifficulty.Easy));
            _overlays.Button(5, 0, FullBtnLayout, _ => LoadPlayerSample(AIDifficulty.Medium));
            _overlays.Button(6, 0, FullBtnLayout, _ => LoadPlayerSample(AIDifficulty.Hard));
            _overlays.Button(7, 0, FullBtnLayout, _ => LoadPlayerSample(AIDifficulty.Expert));

            _overlays.Button(4, 6, FullBtnLayout, _ => SetAIDifficulty(AIDifficulty.Easy));
            _overlays.Button(5, 6, FullBtnLayout, _ => SetAIDifficulty(AIDifficulty.Medium));
            _overlays.Button(6, 6, FullBtnLayout, _ => SetAIDifficulty(AIDifficulty.Hard));
            _overlays.Button(7, 6, FullBtnLayout, _ => SetAIDifficulty(AIDifficulty.Expert));

            _overlays.Button(8, 4, FullBtnLayout,
                _ => { SimulationTime.Instance?.SetTimeScale(1f);
                       SettingsBridge.SetQualityLevel(3); QualityBridge.SetTier(QualityTier.Ultra);
                       SettingsBridge.SetFontSize(20f); SettingsBridge.SetMasterVolume(0.5f);
                       SettingsBridge.SetMusicVolume(0.25f); SettingsBridge.SetSfxVolume(0.75f);
                       SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); });
        }

        private static float SpeedToSlider(float speed) { speed = Mathf.Clamp(speed, 0.1f, 100f); return Mathf.Log10(speed * 10f) / 3f; }
        private static float SliderToSpeed(float slider) { return 0.1f * Mathf.Pow(1000f, Mathf.Clamp01(slider)); }
        private static float FontToSlider(float fontSize) { return Mathf.Clamp01((fontSize - 8f) / 40f); }
        private static float SliderToFont(float slider) { return 8f + Mathf.Clamp01(slider) * 40f; }
        private void ReloadScene() { SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }

        // ═══════════════════════════════════════════════════════════════
        // RENDER
        // ═══════════════════════════════════════════════════════════════

        private void SetN(int r, string[] texts) { Row(r)?.SetNPanelTexts(texts); }

        protected override void Render()
        {
            ClearAllRows();
            if (!_columnsReady) { SetRow(0, BuildCollapsedLine()); return; }
            Row(0)?.SetNPanelTextsCentered(BuildCollapsedRow());
            if (!IsExpanded) return;
            _overlays?.Sync();

            var cols = new string[COL_COUNT][];
            cols[0] = BuildScriptColumn();
            cols[1] = BuildQualityFontColumn();
            cols[2] = BuildMatchColumn();
            cols[3] = BuildTitleColumn();
            cols[4] = BuildControlsColumn();
            cols[5] = BuildAudioColumn();
            cols[6] = BuildAIColumn();

            int maxLines = 0;
            foreach (var col in cols)
                if (col.Length > maxLines) maxLines = col.Length;

            for (int i = 0; i < maxLines; i++)
            {
                int r = i + 1;
                if (r >= totalRows) break;
                var texts = new string[COL_COUNT];
                for (int c = 0; c < COL_COUNT; c++)
                    texts[c] = i < cols[c].Length ? cols[c][i] : "";
                SetN(r, texts);
            }
        }

        private string BuildCollapsedLine()
        {
            if (_match == null) return $" {TUIColors.Bold("CHESS")}";
            string you = TUIColors.Fg(TUIColors.BrightCyan, $"W:{_match.PlayerWins}");
            string them = TUIColors.Fg(TUIColors.BrightMagenta, $"B:{_match.AIWins}");
            return $" {TUIColors.Bold("CHESS")}  {you} {TUIGlyphs.BoxH}{TUIGlyphs.BoxH} {them}";
        }

        private string[] BuildCollapsedRow()
        {
            var t = new string[COL_COUNT];
            string[] labels = { " YOU", " SETTINGS", " MATCH", $" {TUIColors.Bold("CHESS")}", " CONTROLS", " AUDIO", "OPPONENT" };
            string[] dynamic = new string[COL_COUNT];
            dynamic[0] = _match != null ? $" {TUIColors.Fg(TUIColors.BrightCyan, $"W:{_match.PlayerWins}")}" : labels[0];
            dynamic[1] = $" {((QualityTier)SettingsBridge.QualityLevel)}";
            dynamic[2] = _match != null ? $" M:{_match.MatchesPlayed} W:{_match.PlayerWins}" : labels[2];
            dynamic[3] = $" {TUIColors.Bold("♔ MATE")}";
            var sim = SimulationTime.Instance;
            dynamic[4] = sim != null ? $" {sim.GetFormattedTimeScale()}" : labels[4];
            dynamic[5] = $" VOL:{SettingsBridge.MasterVolume * 100:F0}%";
            dynamic[6] = _ai != null && _match != null ? $" {TUIColors.Fg(TUIColors.BrightMagenta, $"AI({_ai.Difficulty}):{_match.AIWins}")}" : labels[6];
            for (int i = 0; i < COL_COUNT; i++)
                t[i] = IsColumnHovered(i) ? (dynamic[i] ?? labels[i]) : labels[i];
            return t;
        }

        // ── Column 0: YOUR SCRIPT ───────────────────────────────

        private string[] BuildScriptColumn()
        {
            var lines = new List<string>();
            if (_playerProgram != null)
            {
                int inst = _playerProgram.Program?.Instructions?.Length ?? 0;
                string status = _playerProgram.IsRunning ? TUIColors.Fg(TUIColors.BrightGreen, "RUN") : TUIColors.Dimmed("STP");
                string tier = _playerScriptTier.HasValue
                    ? TUIColors.Fg(TUIColors.BrightMagenta, $"({_playerScriptTier.Value})")
                    : TUIColors.Dimmed("(custom)");
                lines.Add($"  {status} {TUIColors.Dimmed($"{inst}i")} {tier}");
            }
            else lines.Add(TUIColors.Dimmed("  No program"));

            var diffs = new[] { AIDifficulty.Easy, AIDifficulty.Medium, AIDifficulty.Hard, AIDifficulty.Expert };
            lines.Add("");
            { int cw = ColWidth(0); string l = "LOAD"; lines.Add(new string(' ', Mathf.Max(0, (cw - l.Length) / 2)) + TUIColors.Dimmed(l)); }
            for (int i = 0; i < diffs.Length; i++)
            {
                bool active = _playerScriptTier.HasValue && _playerScriptTier.Value == diffs[i];
                string key = TUIColors.Fg(TUIColors.BrightCyan, $"[F{i + 1}]");
                string label = active ? TUIColors.Fg(TUIColors.BrightGreen, $"{diffs[i]}{TUIGlyphs.ArrowL}") : TUIColors.Dimmed($"{diffs[i]}");
                lines.Add($"  {key} {label}");
            }
            return lines.ToArray();
        }

        // ── Column 6: AI OPPONENT ───────────────────────────────

        private string[] BuildAIColumn()
        {
            var lines = new List<string>();
            if (_ai != null && _ai.Program != null)
            {
                int inst = _ai.Program.Program?.Instructions?.Length ?? 0;
                string status = _ai.Program.IsRunning ? TUIColors.Fg(TUIColors.BrightGreen, "RUN") : TUIColors.Dimmed("STP");
                string diff = TUIColors.Fg(TUIColors.BrightMagenta, $"({_ai.Difficulty})");
                lines.Add($"  {status} {TUIColors.Dimmed($"{inst}i")} {diff}");
            }
            else lines.Add($"  {TUIColors.Fg(TUIColors.BrightYellow, _ai != null ? _ai.Difficulty.ToString() : "?")}");

            var diffs = new[] { AIDifficulty.Easy, AIDifficulty.Medium, AIDifficulty.Hard, AIDifficulty.Expert };
            lines.Add("");
            { int cw = ColWidth(6); string l = "LOAD"; lines.Add(new string(' ', Mathf.Max(0, (cw - l.Length) / 2)) + TUIColors.Dimmed(l)); }
            for (int i = 0; i < diffs.Length; i++)
            {
                bool active = _ai != null && _ai.Difficulty == diffs[i];
                string key = TUIColors.Fg(TUIColors.BrightCyan, $"[S+F{i + 1}]");
                string label = active ? TUIColors.Fg(TUIColors.BrightGreen, $"{diffs[i]}{TUIGlyphs.ArrowL}") : TUIColors.Dimmed($"{diffs[i]}");
                lines.Add($"  {key} {label}");
            }
            return lines.ToArray();
        }

        // ── Column 3: TITLE ─────────────────────────────────────

        private string[] BuildTitleColumn()
        {
            int colW = ColWidth(3);
            var art = BuildAsciiArt(colW);
            int artWidth = art.Length > 0 ? VisibleLen(art[0]) : 0;
            int pad = Mathf.Max(0, (colW - artWidth) / 2);
            if (pad > 0)
            {
                string spaces = new string(' ', pad);
                for (int i = 0; i < art.Length; i++)
                    if (!string.IsNullOrEmpty(art[i])) art[i] = spaces + art[i];
            }
            return art;
        }

        private static int VisibleLen(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int count = 0; bool inTag = false;
            for (int i = 0; i < text.Length; i++)
            { if (text[i] == '<') { inTag = true; continue; } if (text[i] == '>') { inTag = false; continue; } if (!inTag) count++; }
            return count;
        }

        // ── Column 2: MATCH ─────────────────────────────────────

        private string[] BuildMatchColumn()
        {
            var lines = new List<string>();
            if (_match != null)
            {
                string emdash = "\u2014";
                string turn = _match.IsPlayerTurn
                    ? TUIColors.Fg(TUIColors.BrightGreen, "WHITE TURN")
                    : TUIColors.Fg(TUIColors.BrightYellow, "BLACK TURN");
                lines.Add($"  {turn}");
                string check = _match.IsInCheck ? TUIColors.Fg(TUIColors.Red, " CHECK!") : "";
                lines.Add($"  W:{_match.Board.WhitePieces} B:{_match.Board.BlackPieces}{check}");
                lines.Add("");
                int w = ColWidth(2);
                string scoreLabel = "SCORE";
                lines.Add(new string(' ', Mathf.Max(0, (w - scoreLabel.Length) / 2)) + TUIColors.Dimmed(scoreLabel));
                lines.Add($"  {TUIColors.Fg(TUIColors.BrightGreen, $"{_match.PlayerWins}")} {emdash} WIN");
                lines.Add($"  {TUIColors.Fg(TUIColors.BrightMagenta, $"{_match.AIWins}")} {emdash} LOSS");
                lines.Add($"  {TUIColors.Dimmed($"{_match.Draws}")} {emdash} DRAW");
                lines.Add($"  {_match.MatchesPlayed} {emdash} TOTAL");
            }
            else lines.Add(TUIColors.Dimmed("  No match"));
            return lines.ToArray();
        }

        // ── Column 4: CONTROLS ──────────────────────────────────

        private string[] BuildControlsColumn()
        {
            var lines = new List<string>();
            int w = ColWidth(4);
            var sim = SimulationTime.Instance;
            float speed = sim != null ? sim.timeScale : 1f;
            string speedFmt = speed < 10f ? $"{speed:F1}" : $"{speed:F0}";
            string paused = (sim != null && sim.isPaused) ? TUIColors.Fg(TUIColors.BrightYellow, " PAUSED") : "";
            lines.Add(TUIWidgets.AdaptiveSliderRow(w, "SPD", SpeedToSlider(speed), $"{speedFmt,3}x") + paused);
            string pauseLabel = (sim != null && sim.isPaused) ? "PLAY" : "PAUSE";
            lines.Add($" {TUIColors.Fg(TUIColors.BrightCyan, "[P]")} {pauseLabel}");
            lines.Add("");
            int cw = ColWidth(4);
            string infoLabel = "BOARD";
            lines.Add(new string(' ', Mathf.Max(0, (cw - infoLabel.Length) / 2)) + TUIColors.Dimmed(infoLabel));
            if (_match != null)
            {
                lines.Add($"  Material: {_match.Board.WhiteMaterial} vs {_match.Board.BlackMaterial}");
                lines.Add($"  Moves: {_match.Board.TotalMoves}");
                lines.Add($"  50-move: {_match.Board.HalfMoveClock}/100");
            }
            lines.Add($" {TUIColors.Fg(TUIColors.BrightCyan, "[D]")} DEFAULTS");
            return lines.ToArray();
        }

        // ── Column 1: QUALITY / FONT ────────────────────────────

        private string[] BuildQualityFontColumn()
        {
            var lines = new List<string>();
            int w = ColWidth(1);
            float qualNorm = SettingsBridge.QualityLevel / 3f;
            string qualName = ((QualityTier)SettingsBridge.QualityLevel).ToString();
            qualName = qualName.Length > 4 ? qualName.Substring(0, 4) : qualName.PadRight(4);
            lines.Add(TUIWidgets.AdaptiveSliderRow(w, "QTY", qualNorm, qualName));
            lines.Add(TUIWidgets.AdaptiveSliderRow(w, "FNT", FontToSlider(SettingsBridge.FontSize), $"{SettingsBridge.FontSize,2:F0}pt"));
            lines.Add("");
            string sizesLabel = "PARAMS";
            lines.Add(new string(' ', Mathf.Max(0, (w - sizesLabel.Length) / 2)) + TUIColors.Dimmed(sizesLabel));
            if (_bootstrap == null) _bootstrap = FindFirstObjectByType<ChessBootstrap>();
            if (_bootstrap != null)
            {
                lines.Add(TUIWidgets.AdaptiveSliderRow(w, "CEL", (_bootstrap.cellSize - 0.5f) / 1.5f, $"{_bootstrap.cellSize,4:F1}"));
                lines.Add(TUIWidgets.AdaptiveSliderRow(w, "DEP", (_bootstrap.aiDepth - 1f) / 4f, $"{_bootstrap.aiDepth,2}ply"));
            }
            return lines.ToArray();
        }

        // ── Column 5: AUDIO ──────────────────────────────────────

        private string[] BuildAudioColumn()
        {
            var lines = new List<string>();
            int w = ColWidth(5);
            lines.Add(TUIWidgets.AdaptiveSliderRow(w, "VOL", SettingsBridge.MasterVolume, $"{SettingsBridge.MasterVolume * 100:F0}%"));
            lines.Add(TUIWidgets.AdaptiveSliderRow(w, "MSC", SettingsBridge.MusicVolume, $"{SettingsBridge.MusicVolume * 100:F0}%"));
            lines.Add(TUIWidgets.AdaptiveSliderRow(w, "SFX", SettingsBridge.SfxVolume, $"{SettingsBridge.SfxVolume * 100:F0}%"));
            if (_equalizer != null)
            {
                int availH = Mathf.Max(0, totalRows - 1 - lines.Count);
                int eqH = Mathf.Min(6, availH);
                if (eqH >= 1)
                {
                    var eqLines = TUIEqualizer.Render(_equalizer.SmoothedBands, _equalizer.PeakBands,
                        new TUIEqualizer.Config { Width = w, Height = eqH, Style = TUIEqualizer.Style.Bars, ShowBorder = false, ShowPeaks = true, ShowLabels = false });
                    foreach (var line in eqLines) lines.Add(line);
                }
            }
            return lines.ToArray();
        }

        // ═══════════════════════════════════════════════════════════════
        // ASCII ART ENGINE
        // ═══════════════════════════════════════════════════════════════

        private int AsciiPhaseCount => AsciiWordCount * 2;

        private void AdvanceAsciiTimer()
        {
            _asciiTimer += Time.deltaTime;
            bool isHold = (_asciiPhase % 2) == 0;
            float threshold = isHold ? AsciiHold : AsciiAnim;
            if (_asciiTimer >= threshold)
            {
                _asciiTimer = 0f;
                _asciiPhase = (_asciiPhase + 1) % AsciiPhaseCount;
                if ((_asciiPhase % 2) == 1) InitRevealThresholds();
            }
        }

        private void InitRevealThresholds()
        {
            int innerW = AsciiWords[0][0].Length;
            int total = innerW * 5;
            _revealThresholds = new float[total];
            for (int i = 0; i < total; i++) _revealThresholds[i] = UnityEngine.Random.value;
        }

        private string[] BuildAsciiArt(int maxWidth)
        {
            int wordIdx = (_asciiPhase / 2) % AsciiWordCount;
            int innerW = AsciiWords[0][0].Length;
            int clampedInner = Mathf.Min(innerW, Mathf.Max(0, maxWidth - 2));
            if ((_asciiPhase % 2) == 0) return ColorizeWord(AsciiWords[wordIdx], clampedInner);
            int nextIdx = (wordIdx + 1) % AsciiWordCount;
            return DecipherWord(AsciiWords[wordIdx], AsciiWords[nextIdx], clampedInner);
        }

        private string GradientBorderH(char left, char fill, char right, int innerWidth)
        {
            int total = innerWidth + 2;
            var sb = new StringBuilder(total * 32);
            sb.Append(TUIColors.Fg(TUIGradient.CyanMagenta(0f), left.ToString()));
            for (int i = 0; i < innerWidth; i++) { float t = total > 1 ? (float)(i + 1) / (total - 1) : 0f; sb.Append(TUIColors.Fg(TUIGradient.CyanMagenta(t), fill.ToString())); }
            sb.Append(TUIColors.Fg(TUIGradient.CyanMagenta(1f), right.ToString()));
            return sb.ToString();
        }

        private string GradientBorderV(string rawContent)
        {
            var sb = new StringBuilder(rawContent.Length + 128);
            sb.Append(TUIColors.Fg(TUIGradient.CyanMagenta(0f), "║"));
            sb.Append(rawContent);
            sb.Append(TUIColors.Fg(TUIGradient.CyanMagenta(1f), "║"));
            return sb.ToString();
        }

        private string GradientRowRaw(string row, int totalBorderedWidth)
        {
            int len = row.Length; if (len == 0) return "";
            var sb = new StringBuilder(len * 32);
            for (int i = 0; i < len; i++) { float t = totalBorderedWidth > 1 ? (float)(i + 1) / (totalBorderedWidth - 1) : 0f; sb.Append(TUIColors.Fg(TUIGradient.CyanMagenta(t), row[i].ToString())); }
            return sb.ToString();
        }

        private string[] ColorizeWord(string[] word, int innerW)
        {
            int totalW = innerW + 2; var lines = new string[9];
            lines[0] = GradientBorderH('╔', '═', '╗', innerW);
            lines[1] = GradientBorderV(new string(' ', innerW));
            for (int i = 0; i < 5; i++) { string row = word[i].Length > innerW ? word[i].Substring(0, innerW) : word[i].PadRight(innerW); lines[2 + i] = GradientBorderV(GradientRowRaw(row, totalW)); }
            lines[7] = GradientBorderV(new string(' ', innerW));
            lines[8] = GradientBorderH('╚', '═', '╝', innerW);
            return lines;
        }

        private string[] DecipherWord(string[] src, string[] tgt, int innerW)
        {
            float progress = Mathf.Clamp01(_asciiTimer / AsciiAnim); int totalW = innerW + 2; var lines = new string[9];
            lines[0] = GradientBorderH('╔', '═', '╗', innerW);
            lines[1] = GradientBorderV(new string(' ', innerW));
            for (int r = 0; r < 5; r++) { string s = src[r].Length > innerW ? src[r].Substring(0, innerW) : src[r].PadRight(innerW); string t = tgt[r].Length > innerW ? tgt[r].Substring(0, innerW) : tgt[r].PadRight(innerW); lines[2 + r] = GradientBorderV(DecipherRowRaw(s, t, progress, r * innerW, totalW)); }
            lines[7] = GradientBorderV(new string(' ', innerW));
            lines[8] = GradientBorderH('╚', '═', '╝', innerW);
            return lines;
        }

        private string DecipherRowRaw(string src, string tgt, float progress, int threshOffset, int totalBorderedWidth)
        {
            int len = tgt.Length; var sb = new StringBuilder(len * 32);
            for (int i = 0; i < len; i++)
            {
                float t = totalBorderedWidth > 1 ? (float)(i + 1) / (totalBorderedWidth - 1) : 0f;
                char srcCh = i < src.Length ? src[i] : ' '; char tgtCh = tgt[i];
                if (srcCh == tgtCh) { sb.Append(TUIColors.Fg(TUIGradient.CyanMagenta(t), tgtCh.ToString())); continue; }
                int idx = threshOffset + i;
                bool isSettled = _revealThresholds != null && idx < _revealThresholds.Length && progress >= _revealThresholds[idx];
                char ch; if (isSettled) ch = tgtCh; else { bool hasContent = srcCh != ' ' || tgtCh != ' '; ch = hasContent ? GlitchGlyphs[UnityEngine.Random.Range(0, GlitchGlyphs.Length)] : ' '; }
                sb.Append(TUIColors.Fg(TUIGradient.CyanMagenta(t), ch.ToString()));
            }
            return sb.ToString();
        }
    }
}
