using UnityEngine;

namespace PlayerStateMachine
{
    public class DefaultState : IState
    {
        private readonly SpriteRenderer _spriteRenderer;
        private readonly Sprite _stateSprite;
        private readonly Player _player;
        
        
        public DefaultState(Sprite sprite, SpriteRenderer spriteRenderer, Player player)
        {
            _stateSprite = sprite;
            _spriteRenderer = spriteRenderer;
            _player = player;
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