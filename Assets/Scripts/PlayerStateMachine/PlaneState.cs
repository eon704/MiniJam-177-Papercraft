using System.Collections.Generic;
using UnityEngine;

namespace PlayerStateMachine
{
    public class PlaneState : IState
    {
        private readonly Sprite _stateSprite;
        private readonly SpriteRenderer _spriteRenderer;
        private readonly Player _player;
        
        public Player.StateType StateType => Player.StateType.Plane;
        
        public List<Vector2Int> MoveOptions => new()
        {
            new Vector2Int(-1, 1),
            new Vector2Int(-1, -1),
            new Vector2Int(1, 1),
            new Vector2Int(1, -1)
        };

        public List<Cell.TerrainType> MoveTerrain => new()
        {
            Cell.TerrainType.Default, 
            Cell.TerrainType.Fire,
            Cell.TerrainType.Start,
            Cell.TerrainType.End,
        };
        
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
            _player.planeStateSpritePreview.SetActive(true);
            _player.ChangeStateEffect.GetComponent<Animator>().SetTrigger("ChangeState");
            _spriteRenderer.sprite = _stateSprite;
        }

        public void OnExit()
        {
            _player.planeStateSpritePreview.SetActive(false);
        }
    }
}