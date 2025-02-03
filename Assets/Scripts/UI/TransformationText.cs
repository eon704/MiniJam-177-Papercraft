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
        this.player.OnTransformation.AddListener(this.OnTransformation);
    }
    
    private void OnTransformation(Player.StateType stateType, int transformationsLeft)
    {
        if (stateType != this.stateType) return;
        
        print("Upgrading " + stateType + " to " + transformationsLeft);
        this.text.text = transformationsLeft.ToString("D2");
    }
}