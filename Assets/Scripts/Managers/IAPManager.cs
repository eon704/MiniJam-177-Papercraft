using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;


public class IAPService : Singleton<IAPService>
{
    public StoreController m_StoreController { get; private set; }
    private const string noAdsProductId = "remove_ads";

    private void Start()
    {
        InitializeIAP();
    }

    async void InitializeIAP()
    {
        m_StoreController = UnityIAPServices.StoreController();

        m_StoreController.OnPurchasePending += _ => Debug.Log("Purchase pending");
        m_StoreController.OnProductsFetched += OnProductsFetched;
        m_StoreController.OnPurchasesFetched += OnPurchasesFetched;
        m_StoreController.OnPurchaseFailed += _ => Debug.Log($"Purchase failed");;
        m_StoreController.OnPurchaseConfirmed += OnPurchaseConfirmed;
        m_StoreController.OnStoreDisconnected += _ => Debug.Log("Store disconnected");

        await m_StoreController.Connect();

        var productsToFetch = new List<ProductDefinition>
        {
            new(noAdsProductId, ProductType.NonConsumable)
        };

        m_StoreController.FetchProducts(productsToFetch);
    }

    void OnProductsFetched(List<Product> products)
    {
        Debug.Log("Products fetched:");
        foreach (var product in products)
        {
            Debug.Log($"{product.definition.id} - {product.metadata.localizedPriceString}");
        }
        m_StoreController.FetchPurchases();
    }

    void OnPurchasesFetched(Orders orders)
    {
        foreach (ConfirmedOrder order in orders.ConfirmedOrders)
        {
            var product = order.Info.PurchasedProductInfo.First();
            Debug.Log($"Purchased product: {product.productId}");
            if (product.productId == noAdsProductId)
            {
                // Grant entitlement
                AdManager.Instance.DisableAds();
            }
        }
    }

    void OnPurchaseConfirmed(Order order)
    {
        var productId = order.Info.PurchasedProductInfo.First().productId;
        Debug.Log("Order Confirmed: " + productId);
        if (productId == noAdsProductId)
        {
            AdManager.Instance.DisableAds();
        }
    }
}