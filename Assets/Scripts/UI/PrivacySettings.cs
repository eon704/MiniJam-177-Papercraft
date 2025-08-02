using UnityEngine;
using UnityEngine.UI;

public class PrivacySettings : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Toggle consentToggle;
    [SerializeField] private Button changeConsentButton;
    [SerializeField] private Button privacyPolicyButton;
    [SerializeField] private Text consentStatusText;
    
    [Header("Privacy Policy")]
    [SerializeField] private string privacyPolicyURL = "https://yourwebsite.com/privacy-policy";

    private void Start()
    {
        // Setup button listeners
        if (changeConsentButton != null)
            changeConsentButton.onClick.AddListener(OnChangeConsent);
            
        if (privacyPolicyButton != null)
            privacyPolicyButton.onClick.AddListener(OnOpenPrivacyPolicy);
            
        if (consentToggle != null)
            consentToggle.onValueChanged.AddListener(OnConsentToggleChanged);

        // Update UI with current consent status
        UpdateConsentUI();
    }

    private void OnEnable()
    {
        UpdateConsentUI();
    }

    private void UpdateConsentUI()
    {
        if (AdManager.Instance == null) return;

        bool hasConsent = AdManager.Instance.HasUserConsent;
        bool isChecked = AdManager.Instance.IsConsentChecked;

        // Update toggle
        if (consentToggle != null)
        {
            consentToggle.SetIsOnWithoutNotify(hasConsent);
            consentToggle.interactable = isChecked;
        }

        // Update status text
        if (consentStatusText != null)
        {
            if (!isChecked)
            {
                consentStatusText.text = "Consent not yet provided";
            }
            else if (hasConsent)
            {
                consentStatusText.text = "Ads consent: Granted";
            }
            else
            {
                consentStatusText.text = "Ads consent: Declined";
            }
        }
    }

    private void OnConsentToggleChanged(bool value)
    {
        if (AdManager.Instance != null)
        {
            AdManager.Instance.SetUserConsent(value);
            UpdateConsentUI();
        }
    }

    private void OnChangeConsent()
    {
        if (AdManager.Instance != null)
        {
            AdManager.Instance.ShowConsentDialog();
        }
    }

    private void OnOpenPrivacyPolicy()
    {
        if (!string.IsNullOrEmpty(privacyPolicyURL))
        {
            Application.OpenURL(privacyPolicyURL);
        }
    }
}
