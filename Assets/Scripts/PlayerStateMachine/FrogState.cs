using System.Collections.Generic;
using UnityEngine;

namespace PlayerStateMachine
{
    public class FrogState : IState

    {
        private readonly Sprite _stateSprite;
        private readonly SpriteRenderer _spriteRenderer;
        private readonly Player _player;
        
        public Player.StateType StateType => Player.StateType.Frog;
        
        public List<Vector2Int> MoveOptions => new()
        {
            new Vector2Int(0, 2),
            new Vector2Int(0, -2),
            new Vector2Int(2, 0),
            new Vector2Int(-2, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(1, 0),
            new Vector2Int(0, -1),
            new Vector2Int(0, 1)
        };

        public List<Cell.TerrainType> MoveTerrain => new()
        {
            Cell.TerrainType.Default, 
            Cell.TerrainType.Fire,
            Cell.TerrainType.Start,
            Cell.TerrainType.End,
        };
        
        public FrogState(Sprite sprite, SpriteRenderer spriteRenderer, Player player)
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
            _player.frogStateSpritePreview.SetActive(true);
            _player.ChangeStateEffect.GetComponent<Animator>().SetTrigger("ChangeState");
            _spriteRenderer.sprite = _stateSprite;
        }

        public void OnExit()
        {
            _player.frogStateSpritePreview.SetActive(false);
        }
    }
}