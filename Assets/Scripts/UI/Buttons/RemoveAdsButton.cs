using TMPro;
using UnityEngine.UI;
using UnityEngine;


public class RemoveAdsButton : MonoBehaviour
{
    [SerializeField] private TMP_Text availableText;
    [SerializeField] private TMP_Text pendingText;
    [SerializeField] private TMP_Text purchasedText;
    [SerializeField] private Button button;

    void Start()
    {
        bool noAdsPurchased = (AdManager.Instance.adEventsInstance as MobileAdManager)!.HasNoAds;
        availableText.gameObject.SetActive(!noAdsPurchased);
        pendingText.gameObject.SetActive(false);
        purchasedText.gameObject.SetActive(noAdsPurchased);
        button.interactable = !noAdsPurchased;
    }

    public void OnPurchasePending()
    {
        availableText.gameObject.SetActive(false);
        pendingText.gameObject.SetActive(true);
        purchasedText.gameObject.SetActive(false);
        button.interactable = false;
    }

    public void OnPurchaseFailed()
    {
        availableText.gameObject.SetActive(true);
        pendingText.gameObject.SetActive(false);
        purchasedText.gameObject.SetActive(false);
        button.interactable = true;
    }

    public void OnPurchaseSuccess()
    {
        availableText.gameObject.SetActive(false);
        pendingText.gameObject.SetActive(false);
        purchasedText.gameObject.SetActive(true);
        button.interactable = false;
    }
}