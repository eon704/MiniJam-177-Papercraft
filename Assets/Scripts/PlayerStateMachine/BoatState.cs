using System.Collections.Generic;
using UnityEngine;

namespace PlayerStateMachine
{
    public class BoatState : IState
    {
        private readonly GameObject _stateGameObject;
        private readonly Player _player;

        public Player.StateType StateType => Player.StateType.Boat;

        public List<Vector2Int> MoveOptions => new()
        {
            new Vector2Int(-1, 0),
            new Vector2Int(1, 0),
            new Vector2Int(0, -1),
            new Vector2Int(0, 1)
        };

        public List<TerrainType> MoveTerrain => new()
        {
            TerrainType.Water,
            TerrainType.Fire,
        };

        public BoatState(GameObject gameObject, Player player)
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