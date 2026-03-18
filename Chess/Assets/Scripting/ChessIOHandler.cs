// Copyright CodeGamified 2025-2026
// MIT License — Chess
using CodeGamified.Engine;
using CodeGamified.Time;
using Chess.Game;

namespace Chess.Scripting
{
    /// <summary>
    /// Game I/O handler for Chess — bridges CUSTOM opcodes to board state.
    /// Uses ChessOp (the final 32-opcode layout).
    /// </summary>
    public class ChessIOHandler : IGameIOHandler
    {
        private readonly ChessMatchManager _match;
        private readonly ChessBoard _board;

        public ChessIOHandler(ChessMatchManager match)
        {
            _match = match;
            _board = match.Board;
        }

        public bool PreExecute(Instruction inst, MachineState state) => true;

        public void ExecuteIO(Instruction inst, MachineState state)
        {
            int op = (int)inst.Op - (int)OpCode.CUSTOM_0;

            switch ((ChessOp)op)
            {
                // ── Cell queries (R0=col, R1=row → R0) ──
                case ChessOp.GET_CELL:
                {
                    var p = _board.GetCell((int)state.GetRegister(0), (int)state.GetRegister(1));
                    state.SetRegister(0, p.Raw);
                    break;
                }
                case ChessOp.GET_PIECE_TYPE:
                {
                    var p = _board.GetCell((int)state.GetRegister(0), (int)state.GetRegister(1));
                    state.SetRegister(0, (int)p.Type);
                    break;
                }
                case ChessOp.GET_PIECE_COLOR:
                {
                    var p = _board.GetCell((int)state.GetRegister(0), (int)state.GetRegister(1));
                    state.SetRegister(0, (int)p.Color);
                    break;
                }

                // ── Board state → R0 ──
                case ChessOp.GET_TOTAL_MOVES:
                    state.SetRegister(0, _board.TotalMoves);
                    break;
                case ChessOp.GET_WHITE_PIECES:
                    state.SetRegister(0, _board.WhitePieces);
                    break;
                case ChessOp.GET_BLACK_PIECES:
                    state.SetRegister(0, _board.BlackPieces);
                    break;
                case ChessOp.GET_WHITE_MATERIAL:
                    state.SetRegister(0, _board.WhiteMaterial);
                    break;
                case ChessOp.GET_BLACK_MATERIAL:
                    state.SetRegister(0, _board.BlackMaterial);
                    break;

                // ── Turn / check ──
                case ChessOp.IS_PLAYER_TURN:
                    state.SetRegister(0, _match.IsPlayerTurn ? 1f : 0f);
                    break;
                case ChessOp.IS_IN_CHECK:
                    state.SetRegister(0, _match.IsInCheck ? 1f : 0f);
                    break;
                case ChessOp.GET_GAME_STATE:
                {
                    // 0=playing, 1=checkmate, 2=stalemate, 3=draw(50-move/insufficient)
                    if (_board.IsCheckmate()) state.SetRegister(0, 1f);
                    else if (_board.IsStalemate()) state.SetRegister(0, 2f);
                    else if (_board.IsFiftyMoveRule || _board.IsInsufficientMaterial()) state.SetRegister(0, 3f);
                    else state.SetRegister(0, 0f);
                    break;
                }

                // ── Castling ──
                case ChessOp.CAN_CASTLE_K:
                    state.SetRegister(0, _match.CanCastleKingsideNow() ? 1f : 0f);
                    break;
                case ChessOp.CAN_CASTLE_Q:
                    state.SetRegister(0, _match.CanCastleQueensideNow() ? 1f : 0f);
                    break;

                // ── En passant ──
                case ChessOp.GET_EP_COL:
                    state.SetRegister(0, _board.EnPassantCol);
                    break;
                case ChessOp.GET_EP_ROW:
                    state.SetRegister(0, _board.EnPassantRow);
                    break;

                // ── Legal move enumeration ──
                case ChessOp.GET_LEGAL_MOVE_COUNT:
                    state.SetRegister(0, _match.GetLegalMoveCount());
                    break;
                case ChessOp.GET_MOVE_FROM_COL:
                {
                    var m = _match.GetLegalMoveAt((int)state.GetRegister(0));
                    state.SetRegister(0, m.HasValue ? m.Value.FromCol : -1f);
                    break;
                }
                case ChessOp.GET_MOVE_FROM_ROW:
                {
                    var m = _match.GetLegalMoveAt((int)state.GetRegister(0));
                    state.SetRegister(0, m.HasValue ? m.Value.FromRow : -1f);
                    break;
                }
                case ChessOp.GET_MOVE_TO_COL:
                {
                    var m = _match.GetLegalMoveAt((int)state.GetRegister(0));
                    state.SetRegister(0, m.HasValue ? m.Value.ToCol : -1f);
                    break;
                }
                case ChessOp.GET_MOVE_TO_ROW:
                {
                    var m = _match.GetLegalMoveAt((int)state.GetRegister(0));
                    state.SetRegister(0, m.HasValue ? m.Value.ToRow : -1f);
                    break;
                }
                case ChessOp.GET_MOVE_FLAGS:
                {
                    var m = _match.GetLegalMoveAt((int)state.GetRegister(0));
                    if (!m.HasValue) { state.SetRegister(0, 0f); break; }
                    // Encode flags: 1=capture, 2=castle, 4=promotion, 8=en passant
                    int flags = 0;
                    if (m.Value.IsCapture)   flags |= 1;
                    if (m.Value.IsCastle)    flags |= 2;
                    if (m.Value.IsPromotion) flags |= 4;
                    if (m.Value.IsEnPassant) flags |= 8;
                    state.SetRegister(0, flags);
                    break;
                }
                case ChessOp.GET_MOVE_PROMO_TYPE:
                {
                    var m = _match.GetLegalMoveAt((int)state.GetRegister(0));
                    state.SetRegister(0, m.HasValue ? (int)m.Value.PromotionType : 0f);
                    break;
                }

                // ── Last move ──
                case ChessOp.GET_LAST_FROM_COL:
                    state.SetRegister(0, _match.LastFromCol);
                    break;
                case ChessOp.GET_LAST_FROM_ROW:
                    state.SetRegister(0, _match.LastFromRow);
                    break;
                case ChessOp.GET_LAST_TO_COL:
                    state.SetRegister(0, _match.LastToCol);
                    break;
                case ChessOp.GET_LAST_TO_ROW:
                    state.SetRegister(0, _match.LastToRow);
                    break;

                // ── Game result ──
                case ChessOp.GET_WINNER:
                    state.SetRegister(0, _match.Winner);
                    break;
                case ChessOp.GET_PLAYER_WINS:
                    state.SetRegister(0, _match.PlayerWins);
                    break;
                case ChessOp.GET_AI_WINS:
                    state.SetRegister(0, _match.AIWins);
                    break;
                case ChessOp.GET_INPUT:
                    state.SetRegister(0, ChessInputProvider.Instance != null
                        ? ChessInputProvider.Instance.CurrentInput : -1f);
                    break;

                // ── Commands ──
                case ChessOp.MOVE:
                {
                    int fc = (int)state.GetRegister(0);
                    int fr = (int)state.GetRegister(1);
                    int tc = (int)state.GetRegister(2);
                    int tr = (int)state.GetRegister(3);
                    state.SetRegister(0, _match.DoPlayerMove(fc, fr, tc, tr));
                    break;
                }
                case ChessOp.SET_PROMOTION:
                {
                    _match.SetPromotion((int)state.GetRegister(0));
                    state.SetRegister(0, 1f);
                    break;
                }
            }
        }

        public float GetTimeScale() => SimulationTime.Instance?.timeScale ?? 1f;
        public double GetSimulationTime() => SimulationTime.Instance?.simulationTime ?? 0.0;
    }
}
