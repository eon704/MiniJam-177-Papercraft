using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class PrivacySettings : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button changeConsentButton;
    [SerializeField] private Button privacyPolicyButton;
    [SerializeField] private TMP_Text consentStatusText;
    
    [Header("Privacy Policy")]
    [SerializeField] private string privacyPolicyURL = "https://eon704.com/paperbound/privacy";

    [Header("Localization")]
    [SerializeField] private LocalizedString consentNotProvidedString;
    [SerializeField] private LocalizedString adsConsentGrantedString;
    [SerializeField] private LocalizedString adsConsentDeclinedString;

    private void Start()
    {
        // Setup button listeners
        if (changeConsentButton != null)
            changeConsentButton.onClick.AddListener(OnChangeConsent);
            
        if (privacyPolicyButton != null)
            privacyPolicyButton.onClick.AddListener(OnOpenPrivacyPolicy);
            
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

        // Update status text
        if (consentStatusText != null)
        {
            if (!isChecked)
            {
                consentStatusText.text = consentNotProvidedString.GetLocalizedString();
            }
            else if (hasConsent)
            {
                consentStatusText.text = adsConsentGrantedString.GetLocalizedString();
            }
            else
            {
                consentStatusText.text = adsConsentDeclinedString.GetLocalizedString();
            }
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
