using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : Component {
  public static T Instance { get; private set; }
  [Tooltip("Should this object persist between scenes?")]
  [SerializeField] private bool persistent = true;
    
  protected virtual void Awake() {
    // Delete this object if another instance already exists.
    if (Instance != null && Instance != this as T) {
      Destroy(gameObject);
    }
    else {
      Instance = this as T;
        
      if (persistent)
        DontDestroyOnLoad(gameObject);
    }
  }
}