using System;
using UnityEngine;

[Serializable]
public class Observable<T> : IEquatable<Observable<T>>
{
  [SerializeField] private T value;
  private readonly int hashCode = Guid.NewGuid().GetHashCode();
  
  public Observable() { }

  public Observable(T value)
  {
    this.value = value;
  }

  /// <summary>
  /// Triggers with arguments: this, oldValue, newValue
  /// </summary>
  public Action<Observable<T>, T, T> OnChanged;

  public T Value
  {
    get => this.value;
    set
    {
      T oldValue = this.value;
      this.value = value;
      this.OnChanged?.Invoke(this, oldValue, value);
    }
  }

  public static implicit operator T(Observable<T> observable) => observable.value;

  public override string ToString()
  {
    return this.value.ToString();
  }

  public bool Equals(Observable<T> other)
  {
    return other != null && other.value.Equals(this.value);
  }

  public override bool Equals(object other)
  {
    return other is Observable<T> observable && observable.value.Equals(this.value);
  }

  public override int GetHashCode()
  {
    return this.hashCode;
  }
}