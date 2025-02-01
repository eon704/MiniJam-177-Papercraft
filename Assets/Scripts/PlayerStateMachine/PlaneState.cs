using UnityEngine;

namespace PlayerStateMachine
{
    public class PlaneState : IState
    {
        private readonly Sprite _stateSprite;
        private readonly SpriteRenderer _spriteRenderer;
        public PlaneState(Sprite sprite, SpriteRenderer spriteRenderer)
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