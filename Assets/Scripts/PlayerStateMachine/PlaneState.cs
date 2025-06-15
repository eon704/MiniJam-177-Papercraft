using System.Collections.Generic;
using UnityEngine;

namespace PlayerStateMachine
{
    public class PlaneState : IState
    {
        private readonly GameObject _stateGameObject;
        private readonly Player _player;
        
        public Player.StateType StateType => Player.StateType.Plane;
        
        public List<Vector2Int> MoveOptions => new()
        {
            new Vector2Int(-1, 1),
            new Vector2Int(-1, -1),
            new Vector2Int(1, 1),
            new Vector2Int(1, -1)
        };

        public List<TerrainType> MoveTerrain => new()
        {
            TerrainType.Default, 
            TerrainType.Fire,
            TerrainType.Start,
            TerrainType.End,
        };
        
        public PlaneState(GameObject gameObject, Player player)
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