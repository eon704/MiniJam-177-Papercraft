using UnityEngine;
using UnityEngine.UI;

public class ToggleObject : MonoBehaviour
{
    public GameObject targetObject; // The object to activate/deactivate
    public Button activateButton; // Button to activate the object
    public Button deactivateButton; // Button to deactivate the object

    private void Start()
    {
        activateButton?.onClick.AddListener(ActivateObject);
        deactivateButton.onClick.AddListener(DeactivateObject);
    }

    private void ActivateObject()
    {
        if (targetObject != null)
        {
            targetObject.SetActive(true);
        }
    }

    private void DeactivateObject()
    {
        if (targetObject != null)
        {
            targetObject.SetActive(false);
        }
    }
}