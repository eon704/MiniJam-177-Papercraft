using UnityEngine;
using FMODUnity;

[CreateAssetMenu(fileName = "FMODEvents", menuName = "Game/FMOD Events")]
public class FMODEvents : ScriptableObject
{
    private static FMODEvents _instance;
    public static FMODEvents Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<FMODEvents>("FMODEvents");
            return _instance;
        }
    }

    [Header("SFX")]
    public EventReference click;
    public EventReference changeState;
    public EventReference move;
    public EventReference win;
    public EventReference lose;
    public EventReference ding;
    public EventReference card;
    public EventReference boardSpawn;

    [Header("Music")]
    public EventReference mainMenu;
    public EventReference game;
}
