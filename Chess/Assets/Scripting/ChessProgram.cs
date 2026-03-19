// Copyright CodeGamified 2025-2026
// MIT License — Chess
using UnityEngine;
using CodeGamified.Engine;
using CodeGamified.Engine.Compiler;
using CodeGamified.Engine.Runtime;
using CodeGamified.Time;
using Chess.Game;

namespace Chess.Scripting
{
    /// <summary>
    /// ChessProgram — code-controlled Chess AI.
    /// Subclasses ProgramBehaviour from .engine.
    ///
    /// EXECUTION MODEL (tick-based, deterministic):
    ///   - Each simulation tick (~20 ops/sec sim-time), the script runs from the top
    ///   - Memory (variables) persists across ticks
    ///   - PC resets to 0 each tick
    ///   - Each tick the script reads board state and calls move(fc,fr,tc,tr)
    ///   - Results are IDENTICAL at 0.5x, 1x, 100x speed
    ///
    /// Chess is the most complex game in CodeGamified. Students can implement:
    ///   - Simple: pick first legal move
    ///   - Intermediate: material-based evaluation, capture priority
    ///   - Advanced: minimax, alpha-beta pruning, piece-square tables
    /// </summary>
    public class ChessProgram : ProgramBehaviour
    {
        private ChessMatchManager _match;
        private ChessIOHandler _ioHandler;
        private ChessCompilerExtension _compilerExt;
        private bool _isAISide;

        public const float OPS_PER_SECOND = 20f;
        private float _opAccumulator;

        private const string DEFAULT_CODE = @"# ♔ CHESS — Write your chess AI!
# Your script runs at 20 ops/sec (sim-time).
# When it finishes, it restarts from the top.
# Variables persist — use them to track state.
#
# PIECE ENCODING (raw):
#   0=empty, 1-6=white(P,N,B,R,Q,K), 7-12=black(p,n,b,r,q,k)
# PIECE TYPE: 0=none, 1=pawn, 2=knight, 3=bishop, 4=rook, 5=queen, 6=king
# COLOR: 0=none, 1=white(you), 2=black(AI)
#
# BUILTINS — Cell queries:
#   get_cell(col, row)        → raw piece (0-12)
#   get_piece_type(col, row)  → type (0-6)
#   get_piece_color(col, row) → color (0-2)
#
# BUILTINS — Board state:
#   get_total_moves()         → total half-moves played
#   get_white_pieces()        → white piece count
#   get_black_pieces()        → black piece count
#   get_white_material()      → white material value
#   get_black_material()      → black material value
#
# BUILTINS — Turn / Check:
#   is_player_turn()          → 1 if your turn
#   is_in_check()             → 1 if side to move is in check
#   get_game_state()          → 0=play, 1=mate, 2=stale, 3=draw
#
# BUILTINS — Castling / En passant:
#   can_castle_k()            → 1 if kingside castle legal now
#   can_castle_q()            → 1 if queenside castle legal now
#   get_ep_col()              → en passant target col (-1 if none)
#   get_ep_row()              → en passant target row (-1 if none)
#
# BUILTINS — Legal moves:
#   get_legal_move_count()    → number of legal moves
#   get_move_from_col(i)      → from col of move i
#   get_move_from_row(i)      → from row of move i
#   get_move_to_col(i)        → to col of move i
#   get_move_to_row(i)        → to row of move i
#   get_move_flags(i)         → bitmask: 1=capture 2=castle 4=promo 8=ep
#   get_move_promo_type(i)    → promotion type (2-5, or 0)
#
# BUILTINS — Last move:
#   get_last_from_col()       → last move from col
#   get_last_from_row()       → last move from row
#   get_last_to_col()         → last move to col
#   get_last_to_row()         → last move to row
#
# BUILTINS — Game result:
#   get_winner()              → 0=none, 1=player, 2=AI, 3=draw
#   get_player_wins()         → your wins
#   get_ai_wins()             → AI wins
#   get_input()               → keyboard input (-1 if none)
#
# BUILTINS — Commands:
#   move(fc, fr, tc, tr)      → move piece → 1=ok, 0=fail
#   set_promotion(type)       → set promo type (2=N 3=B 4=R 5=Q)
#
# This starter prefers captures, then first legal move:
turn = is_player_turn()
if turn == 1:
    n = get_legal_move_count()
    i = 0
    best = -1
    while i < n:
        flags = get_move_flags(i)
        if flags >= 1:
            best = i
            i = n
        i = i + 1
    if best < 0:
        best = 0
    if n > 0:
        fc = get_move_from_col(best)
        fr = get_move_from_row(best)
        tc = get_move_to_col(best)
        tr = get_move_to_row(best)
        move(fc, fr, tc, tr)
";

        public string CurrentSourceCode => _sourceCode;
        public System.Action OnCodeChanged;

        public void Initialize(ChessMatchManager match,
                               string initialCode = null, string programName = "ChessAI",
                               bool isAISide = false)
        {
            _match = match;
            _isAISide = isAISide;
            _compilerExt = new ChessCompilerExtension();

            _programName = programName;
            _sourceCode = initialCode ?? DEFAULT_CODE;
            _autoRun = true;

            LoadAndRun(_sourceCode);
        }

        protected override void Update()
        {
            if (_executor == null || _program == null || _isPaused) return;
            if (_match == null || !_match.MatchInProgress || _match.GameOver) return;

            float timeScale = SimulationTime.Instance?.timeScale ?? 1f;
            if (SimulationTime.Instance != null && SimulationTime.Instance.isPaused) return;

            float simDelta = UnityEngine.Time.deltaTime * timeScale;
            _opAccumulator += simDelta * OPS_PER_SECOND;

            int opsToRun = (int)_opAccumulator;
            _opAccumulator -= opsToRun;

            for (int i = 0; i < opsToRun; i++)
            {
                if (_executor.State.IsHalted)
                {
                    _executor.State.PC = 0;
                    _executor.State.IsHalted = false;
                }
                _executor.ExecuteOne();
            }

            if (opsToRun > 0)
                ProcessEvents();
        }

        protected override IGameIOHandler CreateIOHandler()
        {
            _ioHandler = new ChessIOHandler(_match, _isAISide);
            return _ioHandler;
        }

        protected override CompiledProgram CompileSource(string source, string name)
        {
            return PythonCompiler.Compile(source, name, _compilerExt);
        }

        protected override void ProcessEvents()
        {
            if (_executor?.State == null) return;
            while (_executor.State.OutputEvents.Count > 0)
                _executor.State.OutputEvents.Dequeue();
        }

        public void UploadCode(string newSource)
        {
            _sourceCode = newSource ?? DEFAULT_CODE;
            LoadAndRun(_sourceCode);
            Debug.Log($"[ChessAI] Uploaded new code ({_program?.Instructions?.Length ?? 0} instructions)");
            OnCodeChanged?.Invoke();
        }

        public void ResetExecution()
        {
            if (_executor?.State == null) return;
            _executor.State.Reset();
            _opAccumulator = 0f;
        }
    }
}
