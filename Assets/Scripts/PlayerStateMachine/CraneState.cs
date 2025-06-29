using System.Collections.Generic;
using UnityEngine;

namespace PlayerStateMachine
{
    public class CraneState : IState
    {    
        
       
        private readonly GameObject _stateGameObject;
        private readonly Player _player;
        private StateModel stateModel => StateModelInfo.StateModels[StateType];
      
        public Player.StateType StateType => Player.StateType.Crane;

        public List<Vector2Int> MoveOptions => stateModel.MoveOptions;

        public List<TerrainType> MoveTerrain => stateModel.MoveTerrain;
        
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