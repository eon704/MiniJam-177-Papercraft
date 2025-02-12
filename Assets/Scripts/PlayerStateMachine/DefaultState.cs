using System.Collections.Generic;
using UnityEngine;

namespace PlayerStateMachine
{
    public class DefaultState : IState
    {
        private readonly SpriteRenderer _spriteRenderer;
        private readonly Sprite _stateSprite;
        private readonly Player _player;
        
        public Player.StateType StateType => Player.StateType.Default;
        
        public List<Vector2Int> MoveOptions => new();

        public List<Cell.TerrainType> MoveTerrain => new();
        
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
            _player.defaultStateSpritePreview.SetActive(true);
            _spriteRenderer.sprite = _stateSprite;
        }

        public void OnExit()
        {
            _player.defaultStateSpritePreview.SetActive(false);
        }
    }
}