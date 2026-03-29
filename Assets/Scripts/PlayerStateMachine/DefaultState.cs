using System.Collections.Generic;
using UnityEngine;

namespace PlayerStateMachine
{
    public class DefaultState : IState
    {
        private readonly GameObject _defaultPlayer;
        
        public Player.StateType StateType => Player.StateType.Default;
        private StateModel stateModel => StateModelInfo.StateModels[StateType];
        
        public List<Vector2Int> MoveOptions => stateModel.MoveOptions;

        public List<TerrainType> MoveTerrain => stateModel.MoveTerrain;

        public MoveMode MoveMode => MoveMode.Normal;
        
        public DefaultState(GameObject defaultModel)
        {
            _defaultPlayer = defaultModel;
        }
        

        public void Tick()
        {
        }

        public void OnEnter()
        {
            _defaultPlayer.SetActive(true);
        }

        public void OnExit()
        {
            _defaultPlayer.SetActive(false);
        }
    }
}