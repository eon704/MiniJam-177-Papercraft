using UnityEngine;
using UnityEngine.EventSystems;

public class Tooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject tooltipPrefab;
    private GameObject _tooltipInstance;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltipPrefab == null) return;
        _tooltipInstance = Instantiate(tooltipPrefab, transform.root);
        _tooltipInstance.transform.SetAsLastSibling();
        var mousePosition = Input.mousePosition;
        _tooltipInstance.transform.position = mousePosition;

    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_tooltipInstance != null)
        {
            Destroy(_tooltipInstance);
        }
    }
    
}