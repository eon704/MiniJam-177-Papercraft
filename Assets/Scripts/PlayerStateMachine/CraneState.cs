using UnityEngine;

namespace PlayerStateMachine
{
    public class CraneState : IState
    {    
        
        private readonly SpriteRenderer _spriteRenderer;
        private readonly Sprite _stateSprite;
        
        public CraneState(Sprite sprite, SpriteRenderer spriteRenderer)
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