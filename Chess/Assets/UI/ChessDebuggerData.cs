// Copyright CodeGamified 2025-2026
// MIT License — Chess
using System.Collections.Generic;
using UnityEngine;
using CodeGamified.Engine;
using CodeGamified.Engine.Runtime;
using CodeGamified.TUI;
using Chess.Scripting;
using static Chess.Scripting.ChessOp;

namespace Chess.UI
{
    /// <summary>
    /// Adapts a ChessProgram into the engine's IDebuggerDataSource contract.
    /// Fed to DebuggerSourcePanel, DebuggerMachinePanel, DebuggerStatePanel.
    /// </summary>
    public class ChessDebuggerData : IDebuggerDataSource
    {
        private readonly ChessProgram _program;
        private readonly string _label;

        public ChessDebuggerData(ChessProgram program, string label = null)
        {
            _program = program;
            _label = label;
        }

        public string ProgramName => _label ?? _program?.ProgramName ?? "ChessAI";
        public string[] SourceLines => _program?.Program?.SourceLines;
        public bool HasLiveProgram =>
            _program != null && _program.Executor != null && _program.Program != null
            && _program.Program.Instructions != null && _program.Program.Instructions.Length > 0;
        public int PC
        {
            get
            {
                var s = _program?.State;
                if (s == null) return 0;
                return s.LastExecutedPC >= 0 ? s.LastExecutedPC : s.PC;
            }
        }
        public long CycleCount => _program?.State?.CycleCount ?? 0;

        public string StatusString
        {
            get
            {
                if (_program == null || _program.Executor == null)
                    return TUIColors.Dimmed("NO PROGRAM");
                var state = _program.State;
                if (state == null) return TUIColors.Dimmed("NO STATE");
                int instCount = _program.Program?.Instructions?.Length ?? 0;
                return TUIColors.Fg(TUIColors.BrightGreen, $"TICK {instCount} inst");
            }
        }

        public List<string> BuildSourceLines(int pc, int scrollOffset, int maxRows)
        {
            var lines = new List<string>();
            var src = SourceLines;
            if (src == null) return lines;

            int activeLine = -1;
            int activeEnd = -1;
            bool isHalt = false;
            Instruction activeInst = default;
            if (HasLiveProgram && _program.Program.Instructions.Length > 0
                && pc < _program.Program.Instructions.Length)
            {
                activeInst = _program.Program.Instructions[pc];
                activeLine = activeInst.SourceLine - 1;
                isHalt = activeInst.Op == OpCode.HALT;
                if (activeLine >= 0)
                    activeEnd = SourceHighlight.GetContinuationEnd(src, activeLine);
            }

            if (scrollOffset == 0 && lines.Count < maxRows)
            {
                string whileLine = "while True:";
                if (isHalt)
                    lines.Add(TUIColors.Fg(TUIColors.BrightGreen, $"  {TUIGlyphs.ArrowR}   {whileLine}"));
                else
                    lines.Add($"  {TUIColors.Dimmed(TUIGlyphs.ArrowR)}   {SynthwaveHighlighter.Highlight(whileLine)}");
            }

            int tokenLine = -1;
            if (activeLine >= 0)
            {
                string token = SourceHighlight.GetSourceToken(activeInst);
                if (token != null)
                {
                    for (int k = activeLine; k <= activeEnd; k++)
                    {
                        if (src[k].IndexOf(token) >= 0) { tokenLine = k; break; }
                    }
                }
                if (tokenLine < 0) tokenLine = activeLine;
            }

            for (int i = scrollOffset; i < src.Length && lines.Count < maxRows; i++)
            {
                if (i == tokenLine)
                {
                    lines.Add(SourceHighlight.HighlightActiveLine(
                        src[i], $" {i + 1:D3}      ", activeInst));
                }
                else
                {
                    string num = TUIColors.Dimmed($"{i + 1:D3}");
                    lines.Add($" {num}      {SynthwaveHighlighter.Highlight(src[i])}");
                }
            }
            return lines;
        }

        public List<string> BuildMachineLines(int pc, int maxRows)
        {
            var lines = new List<string>();
            if (!HasLiveProgram) return lines;

            var instructions = _program.Program.Instructions;
            int total = instructions.Length;

            int offset = 0;
            if (total > maxRows)
                offset = Mathf.Clamp(pc - maxRows / 3, 0, total - maxRows);
            int visibleCount = Mathf.Min(maxRows, total);

            for (int j = 0; j < visibleCount; j++)
            {
                int i = offset + j;
                var inst = instructions[i];
                bool isPC = (i == pc);
                string asm = inst.ToAssembly(FormatChessOp);
                if (isPC)
                    lines.Add(TUIColors.Fg(TUIColors.BrightGreen, $" {i:X3}  {asm}"));
                else
                {
                    string addr = TUIColors.Dimmed($"{i:X3}");
                    lines.Add($" {addr}  {SynthwaveHighlighter.HighlightAsm(asm)}");
                }
            }
            return lines;
        }

        public List<string> BuildStateLines()
        {
            if (!HasLiveProgram) return new List<string>();
            var s = _program.State;
            int displayPC = s.LastExecutedPC >= 0 ? s.LastExecutedPC : s.PC;
            return TUIWidgets.BuildStateLines(
                s.Registers, s.LastRegisterModified,
                s.Flags, displayPC, s.Stack.Count,
                s.NameToAddress, s.Memory);
        }

        static string FormatChessOp(Instruction inst)
        {
            int id = (int)inst.Op - (int)OpCode.CUSTOM_0;
            return (ChessOp)id switch
            {
                GET_CELL              => "INP R0, CELL(R0,R1)",
                GET_PIECE_TYPE        => "INP R0, TYPE(R0,R1)",
                GET_PIECE_COLOR       => "INP R0, CLR(R0,R1)",
                GET_TOTAL_MOVES       => "INP R0, MOVES",
                GET_WHITE_PIECES      => "INP R0, W.PCS",
                GET_BLACK_PIECES      => "INP R0, B.PCS",
                GET_WHITE_MATERIAL    => "INP R0, W.MAT",
                GET_BLACK_MATERIAL    => "INP R0, B.MAT",
                IS_PLAYER_TURN        => "INP R0, MY.TRN",
                IS_IN_CHECK           => "INP R0, CHECK",
                GET_GAME_STATE        => "INP R0, STATE",
                CAN_CASTLE_K          => "INP R0, CAS.K",
                CAN_CASTLE_Q          => "INP R0, CAS.Q",
                GET_EP_COL            => "INP R0, EP.COL",
                GET_EP_ROW            => "INP R0, EP.ROW",
                GET_LEGAL_MOVE_COUNT  => "INP R0, MV.CNT",
                GET_MOVE_FROM_COL     => "INP R0, MV.FC",
                GET_MOVE_FROM_ROW     => "INP R0, MV.FR",
                GET_MOVE_TO_COL       => "INP R0, MV.TC",
                GET_MOVE_TO_ROW       => "INP R0, MV.TR",
                GET_MOVE_FLAGS        => "INP R0, MV.FLG",
                GET_MOVE_PROMO_TYPE   => "INP R0, MV.PRO",
                GET_LAST_FROM_COL     => "INP R0, LST.FC",
                GET_LAST_FROM_ROW     => "INP R0, LST.FR",
                GET_LAST_TO_COL       => "INP R0, LST.TC",
                GET_LAST_TO_ROW       => "INP R0, LST.TR",
                GET_WINNER            => "INP R0, WINNER",
                GET_PLAYER_WINS       => "INP R0, P.WIN",
                GET_AI_WINS           => "INP R0, AI.WIN",
                GET_INPUT             => "INP R0, INPUT",
                MOVE                  => "OUT MOVE, R0-R3",
                SET_PROMOTION         => "OUT PROMO, R0",
                _                     => $"IO.{id,2} {inst.Arg0}, {inst.Arg1}"
            };
        }
    }
}
