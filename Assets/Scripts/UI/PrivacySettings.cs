
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;

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

    [Header("Consent Dialog")]
    [SerializeField] private ConsentDialog consentDialog;

#if UNITY_IOS || UNITY_ANDROID
    private void Start()
    {
        // Setup button listeners
        changeConsentButton.onClick.AddListener(OnChangeConsent);
        privacyPolicyButton.onClick.AddListener(OnOpenPrivacyPolicy);
        // Update UI with current consent status
        UpdateConsentUI();
    }

    private void OnEnable()
    {
        UpdateConsentUI();
        consentDialog.OnConsentChanged.AddListener(UpdateConsentUI);
    }

    private void OnDisable()
    {
        consentDialog.OnConsentChanged.RemoveListener(UpdateConsentUI);
    }

    private void UpdateConsentUI()
    {
        if (AdManager.Instance == null) return;

        MobileAdManager mobileAdManager = AdManager.Instance.adEventsInstance as MobileAdManager;
        bool hasConsent = mobileAdManager.HasUserConsent;
        bool isChecked = mobileAdManager.IsConsentChecked;

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
        consentDialog.ShowConsentDialog();
    }

    private void OnOpenPrivacyPolicy()
    {
        if (!string.IsNullOrEmpty(privacyPolicyURL))
        {
            Application.OpenURL(privacyPolicyURL);
        }
    }
#endif
}