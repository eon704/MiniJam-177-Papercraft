using System.Collections.Generic;
using UnityEngine;

namespace PlayerStateMachine
{
    public class DefaultState : IState
    {
        private readonly SpriteRenderer _spriteRenderer;
        private readonly Sprite _stateSprite;
        
        public Player.StateType StateType => Player.StateType.Default;
        
        public List<Vector2Int> MoveOptions => new();

        public List<TerrainType> MoveTerrain => new();
        
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
            _spriteRenderer.sprite = null;
        }
    }
}