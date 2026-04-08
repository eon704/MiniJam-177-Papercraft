using System.Collections.Generic;
using UnityEngine;

/// <summary>Baked physics recording for a set of fragments. Stores local-space positions and rotations per frame.</summary>
[CreateAssetMenu(fileName = "FragmentRecording", menuName = "Game/Fragment Recording")]
public class FragmentRecording : ScriptableObject
{
    public float frameInterval = 0.02f;
    public FragmentTrack[] tracks; // одна дорожка на каждый фрагмент
}

[System.Serializable]
public class FragmentTrack
{
    public List<Vector3>    positions  = new List<Vector3>();
    public List<Quaternion> rotations  = new List<Quaternion>();
}
