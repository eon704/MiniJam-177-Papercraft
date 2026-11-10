using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class Tooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject tooltipPrefab;
    private GameObject _tooltipInstance;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltipPrefab == null) return;
        _tooltipInstance = Instantiate(tooltipPrefab, transform.root);
        _tooltipInstance.transform.SetAsLastSibling();
        // Project uses the new Input System only; UnityEngine.Input would throw here.
        Vector2 pointerPosition = eventData != null ? eventData.position
            : Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        _tooltipInstance.transform.position = pointerPosition;

    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_tooltipInstance != null)
        {
            Destroy(_tooltipInstance);
        }
    }
    
}