using UnityEngine;

namespace PlayerStateMachine
{
    public class BoatState : IState
    {
        private readonly Sprite _stateSprite;
        private readonly SpriteRenderer _spriteRenderer;

        public BoatState(Sprite sprite, SpriteRenderer spriteRenderer)
        {
            _stateSprite = sprite;
            _spriteRenderer = spriteRenderer;
        }

        public void Tick()
        {
        }

        public void OnEnter()
        {
            _spriteRenderer.sprite = _stateSprite;
        }

        public void OnExit()
        {
        }
    }
}