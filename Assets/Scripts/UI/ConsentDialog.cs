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

#if UNITY_IOS || UNITY_ANDROID
    private void Awake()
    {
        acceptButton.onClick.AddListener(OnAcceptConsent);
        declineButton.onClick.AddListener(OnDeclineConsent);
        privacyPolicyButton.onClick.AddListener(OnOpenPrivacyPolicy);

        consentPanel.SetActive(false);
    }

    private void Start()
    {
        AdManager.Instance.adEventsInstance.OnConsentRequired.AddListener(ShowConsentDialog);

        // Trigger consent check as a backup in case the timing was off
        MobileAdManager mobileAdManager = AdManager.Instance.adEventsInstance as MobileAdManager;
        mobileAdManager.TriggerConsentCheckIfNeeded();
    }

    private void OnDestroy()
    {
        // Cleanup listeners
        if (AdManager.Instance != null)
        {
            AdManager.Instance.adEventsInstance.OnConsentRequired.RemoveListener(ShowConsentDialog);
        }
    }

    public void ShowConsentDialog()
    {
        consentPanel.SetActive(true);
    }

    public void HideConsentDialog()
    {
        consentPanel.SetActive(false);
    }

    private void OnAcceptConsent()
    {
        MobileAdManager mobileAdManager = AdManager.Instance.adEventsInstance as MobileAdManager;
        mobileAdManager.SetUserConsent(true);

        HideConsentDialog();
        OnConsentChanged?.Invoke();
    }

    private void OnDeclineConsent()
    {
        MobileAdManager mobileAdManager = AdManager.Instance.adEventsInstance as MobileAdManager;
        mobileAdManager.SetUserConsent(false);

        HideConsentDialog();
        OnConsentChanged?.Invoke();
    }

    private void OnOpenPrivacyPolicy()
    {
        Application.OpenURL(privacyPolicyURL);
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
        MobileAdManager mobileAdManager = AdManager.Instance.adEventsInstance as MobileAdManager;
        mobileAdManager.RevokeConsent();
    }
#endif
}