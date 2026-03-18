// Copyright CodeGamified 2025-2026
// MIT License — Chess
using UnityEngine;
using UnityEngine.InputSystem;

namespace Chess.Scripting
{
    /// <summary>
    /// Captures input for Chess.
    /// Outputs move index (0-based) from legal move list, or -1.
    /// Keys 0-9 select move index. For code play, scripts call move() directly.
    /// </summary>
    public class ChessInputProvider : MonoBehaviour
    {
        public static ChessInputProvider Instance { get; private set; }

        public float CurrentInput { get; private set; }

        private InputAction[] _numActions;

        private void Awake()
        {
            Instance = this;

            _numActions = new InputAction[10];
            string[] keys = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };
            for (int i = 0; i < 10; i++)
            {
                _numActions[i] = new InputAction($"Num{i}", InputActionType.Button);
                _numActions[i].AddBinding($"<Keyboard>/{keys[i]}");
                _numActions[i].Enable();
            }
        }

        private void Update()
        {
            for (int i = 0; i < 10; i++)
            {
                if (_numActions[i].WasPressedThisFrame())
                {
                    CurrentInput = i;
                    return;
                }
            }
            CurrentInput = -1f;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < 10; i++)
            {
                _numActions[i]?.Disable();
                _numActions[i]?.Dispose();
            }
            if (Instance == this) Instance = null;
        }
    }
}
