using System.Collections;
using TMPro;
using UnityEngine;

public class TransformationText : MonoBehaviour
{
    [SerializeField] private Player.StateType stateType;
    [SerializeField] private TMP_Text text;
    [SerializeField] private Player player;

    private void Start()
    {
        this.player.OnMovesLeftChanged.AddListener(this.OnTransformation);
    }
    
    private void OnTransformation(Player.StateType stateType, int transformationsLeft)
    {
        if (stateType != this.stateType) return;
        this.text.text = transformationsLeft.ToString("D2");
    }
}