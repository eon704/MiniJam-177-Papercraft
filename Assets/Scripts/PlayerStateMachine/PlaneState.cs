using UnityEngine;

namespace PlayerStateMachine
{
    public class PlaneState : IState
    {
        private readonly Sprite _stateSprite;
        private readonly SpriteRenderer _spriteRenderer;
        private readonly Player _player;
        public PlaneState(Sprite sprite, SpriteRenderer spriteRenderer, Player player)
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
            _player.changeStateEffect.GetComponent<Animator>().SetTrigger("ChangeState");
            _spriteRenderer.sprite = _stateSprite;
        }

        public void OnExit()
        {
        }
    }
}