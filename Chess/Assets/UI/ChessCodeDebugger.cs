// Copyright CodeGamified 2025-2026
// MIT License — Chess
using CodeGamified.TUI;
using Chess.Scripting;

namespace Chess.UI
{
    /// <summary>
    /// Thin adapter — wires a ChessProgram into the engine's CodeDebuggerWindow
    /// via ChessDebuggerData (IDebuggerDataSource). All rendering lives in the engine.
    /// </summary>
    public class ChessCodeDebugger : CodeDebuggerWindow
    {
        protected override void Awake()
        {
            base.Awake();
            windowTitle = "CODE";
        }

        public void Bind(ChessProgram program)
        {
            SetDataSource(new ChessDebuggerData(program));
        }
    }
}
