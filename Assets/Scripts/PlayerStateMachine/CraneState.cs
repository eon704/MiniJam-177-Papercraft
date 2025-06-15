using System.Collections.Generic;
using UnityEngine;

namespace PlayerStateMachine
{
    public class CraneState : IState
    {    
        
       
        private readonly GameObject _stateGameObject;
        private readonly Player _player;
      
        public Player.StateType StateType => Player.StateType.Crane;
        
        public List<Vector2Int> MoveOptions => new()
        {
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0)
        };

        public List<TerrainType> MoveTerrain => new()
        {
            TerrainType.Default, 
            TerrainType.Stone, 
            TerrainType.Fire, 
            TerrainType.Start,
            TerrainType.End,
        };
        
        public CraneState(GameObject gameObject, Player player)
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