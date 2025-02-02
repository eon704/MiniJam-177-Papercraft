using System.Collections.Generic;
using UnityEngine;

namespace PlayerStateMachine
{
    public class CraneState : IState
    {    
        
        private readonly SpriteRenderer _spriteRenderer;
        private readonly Sprite _stateSprite;
        private readonly Player _player;
      
        public List<Vector2Int> MoveOptions => new()
        {
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0)
        };

        public List<Cell.TerrainType> MoveTerrain => new()
            { Cell.TerrainType.Default, Cell.TerrainType.Stone, Cell.TerrainType.Fire };
        
        public CraneState(Sprite sprite, SpriteRenderer spriteRenderer, Player player)
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