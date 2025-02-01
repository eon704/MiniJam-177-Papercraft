using UnityEngine;

namespace PlayerStateMachine
{
    public class DefaultState : IState
    {
        private readonly SpriteRenderer _spriteRenderer;
        private readonly Sprite _stateSprite;
        
        public DefaultState(Sprite sprite, SpriteRenderer spriteRenderer)
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