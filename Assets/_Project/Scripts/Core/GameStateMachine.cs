using System;
using UnityEngine;

namespace MathGame.Core
{
    public class GameStateMachine : MonoBehaviour
    {
        public GamePhase CurrentPhase { get; private set; } = GamePhase.Idle;

        public event Action<GamePhase, GamePhase> OnPhaseChanged;

        public void TransitionTo(GamePhase newPhase)
        {
            if (newPhase == CurrentPhase) return;

            GamePhase previous = CurrentPhase;
            CurrentPhase = newPhase;

            OnPhaseChanged?.Invoke(previous, newPhase);
        }
    }
}
