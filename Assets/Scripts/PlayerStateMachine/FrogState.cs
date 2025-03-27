using System.Collections.Generic;
using UnityEngine;

namespace PlayerStateMachine
{
    public class FrogState : IState

    {
        private readonly GameObject _stateGameObject;
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
        
        public FrogState(GameObject gameObject, Player player)
        {
            _stateGameObject = gameObject;
            _player = player;
        }

        public void Tick()
        {
        }

        public void OnEnter()
        {
           
            _player.changeStateEffect.GetComponent<Animator>().SetTrigger("ChangeState");
            _stateGameObject.SetActive(true);
        }

        public void OnExit()
        {
            _stateGameObject.SetActive(false);
            
        }
    }
}