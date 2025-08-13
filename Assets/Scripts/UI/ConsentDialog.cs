#if UNITY_IOS || UNITY_ANDROID

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class ConsentDialog : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject consentPanel;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button declineButton;
    [SerializeField] private Button privacyPolicyButton;

    [Header("Privacy Policy")]
    [SerializeField] private string privacyPolicyURL = "https://eon704.com/paperbound/privacy";

    [Header("Events")]
    public UnityEvent OnConsentChanged;

    private void Awake()
    {
        // Setup button listeners
        if (acceptButton != null)
            acceptButton.onClick.AddListener(OnAcceptConsent);

        if (declineButton != null)
            declineButton.onClick.AddListener(OnDeclineConsent);

        if (privacyPolicyButton != null)
            privacyPolicyButton.onClick.AddListener(OnOpenPrivacyPolicy);

        // Hide the panel initially
        if (consentPanel != null)
            consentPanel.SetActive(false);
    }

    private void Start()
    {
        // Listen for consent requests from AdManager
        if (AdManager.Instance != null)
        {
            AdManager.Instance.OnConsentRequired.AddListener(ShowConsentDialog);

            // Trigger consent check as a backup in case the timing was off
            AdManager.Instance.TriggerConsentCheckIfNeeded();
        }
    }

    private void OnDestroy()
    {
        // Cleanup listeners
        if (AdManager.Instance != null)
        {
            AdManager.Instance.OnConsentRequired.RemoveListener(ShowConsentDialog);
        }
    }

    public void ShowConsentDialog()
    {
        if (consentPanel != null)
        {
            consentPanel.SetActive(true);
        }
    }

    public void HideConsentDialog()
    {
        if (consentPanel != null)
        {
            consentPanel.SetActive(false);
        }
    }

    private void OnAcceptConsent()
    {
        if (AdManager.Instance != null)
        {
            AdManager.Instance.SetUserConsent(true);
        }
        HideConsentDialog();
        OnConsentChanged?.Invoke();
    }

    private void OnDeclineConsent()
    {
        if (AdManager.Instance != null)
        {
            AdManager.Instance.SetUserConsent(false);
        }
        HideConsentDialog();
        OnConsentChanged?.Invoke();
    }

    private void OnOpenPrivacyPolicy()
    {
        if (!string.IsNullOrEmpty(privacyPolicyURL))
        {
            Application.OpenURL(privacyPolicyURL);
        }
    }

    /// <summary>
    /// Call this from a settings menu to allow users to change consent
    /// </summary>
    public void ShowConsentSettings()
    {
        ShowConsentDialog();
    }

    /// <summary>
    /// Call this to revoke consent (e.g., from settings menu)
    /// </summary>
    public void RevokeConsent()
    {
        if (AdManager.Instance != null)
        {
            AdManager.Instance.RevokeConsent();
        }
    }
}
#endif