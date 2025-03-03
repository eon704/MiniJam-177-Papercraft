using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : Component {
  public static T Instance { get; private set; }
  [Tooltip("Should this object persist between scenes?")]
  [SerializeField] private bool persistent = true;
    
  protected virtual void Awake() {
    // Delete this object if another instance already exists.
    if (Singleton<T>.Instance != null && Singleton<T>.Instance != this as T) {
      Destroy(this.gameObject);
    }
    else {
      Singleton<T>.Instance = this as T;
        
      if (this.persistent)
        DontDestroyOnLoad(this.gameObject);
    }
  }
}