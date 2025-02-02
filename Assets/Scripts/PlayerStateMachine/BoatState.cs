using System.Collections.Generic;
using UnityEngine;

namespace PlayerStateMachine
{
    public class BoatState : IState
    {
        private readonly Sprite _stateSprite;
        private readonly SpriteRenderer _spriteRenderer;
        private readonly Player _player;

        public List<Vector2Int> MoveOptions => new()
        {
            new Vector2Int(-1, 0),
            new Vector2Int(1, 0),
            new Vector2Int(0, -1),
            new Vector2Int(0, 1)
        };

        public List<Cell.TerrainType> MoveTerrain => new() { Cell.TerrainType.Water };

        public BoatState(Sprite sprite, SpriteRenderer spriteRenderer, Player player)
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