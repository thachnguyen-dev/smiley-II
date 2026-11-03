using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if !UNITY_WEBGL
using UnityEngine.Purchasing;
#endif

namespace Smiley.Ads
{
    /// <summary>
    /// The Remove Ads purchase (D-124): one non-consumable product, bought from the Main Menu.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⭐ Owner, 2026-10-01: a <b>$1.99 Remove Ads</b> purchase that removes <b>all</b>
    /// advertising, offered on the <b>Main Menu</b>. ⚠ This amends <b>D-003</b>, which made
    /// banner advertising the sole monetisation and listed a shop among the forbidden patterns.
    /// </para>
    /// <para>
    /// ⛔ <b>THIS IS THE ONLY FILE IN THE PROJECT THAT NAMES A UNITY PURCHASING TYPE.</b>
    /// Everything else sees <see cref="RemoveAdsRule"/> and <see cref="PurchasedState"/>. ⛔ Every
    /// call here was read from the installed <c>com.unity.purchasing</c> 5.4.3 package source and
    /// its own "Minimal Coded IAP 5.0.0" sample, not from memory — this API was rewritten between
    /// 4.x and 5.x and recall of it is exactly what <c>VERSION.md</c> warns about.
    /// </para>
    /// <para>
    /// ⛔ <b>Nothing in the game waits on the store.</b> A phone in aeroplane mode, a store that
    /// never connects, a product that does not resolve and a cancelled purchase are all normal
    /// code paths: the button simply does not complete, and the game is untouched. That is
    /// <b>D-003</b>'s connection rule, which the purchase does not get to break.
    /// </para>
    /// <para>
    /// ⚠ <b>The store is the truth; the settings file is the memory.</b> A confirmed purchase is
    /// written to the settings so the game knows while offline and before the store connects;
    /// <see cref="UnityEngine.Purchasing.StoreController.FetchPurchases"/> on every launch
    /// restores it after a reinstall or on a second device. ⛔ No defence against someone editing
    /// that file — it is their own device and this game has no server.
    /// </para>
    /// <para>
    /// ⓘ In the Editor the store is never touched. Unity's editor store is a fake that opens a
    /// dialog, which a batch-mode fixture cannot answer; every test therefore exercises the
    /// "no store" path, which is the one that must never block the game.
    /// </para>
    /// <para>
    /// ⭐ <b>D-138: on the web there is no store, and no Unity Purchasing line is compiled.</b>
    /// <see cref="ProductAvailable"/> never becomes true there, so <see cref="ShouldOffer"/> is
    /// false and the Main Menu shows no Remove Ads button — never one with nothing behind it.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class StorePurchases : MonoBehaviour
    {
        [SerializeField] private AdSettings _settings;

#if !UNITY_WEBGL
        private StoreController _store;
#endif
        private bool _connecting;

        /// <summary>The one instance, alive across both scenes.</summary>
        public static StorePurchases Instance { get; private set; }

        /// <summary>What the store is doing, for tests and the log.</summary>
        public string Status { get; private set; } = "idle";

        /// <summary>Raised when the purchase is granted or restored, so the menu can redraw.</summary>
        public event Action Changed;

        /// <summary>Whether ads have been bought away.</summary>
        public bool Purchased => PurchasedState.AdsRemoved;

        /// <summary>Whether there is a product configured to sell at all.</summary>
        public bool IsConfigured =>
            _settings != null && !string.IsNullOrWhiteSpace(_settings.RemoveAdsProductId);

        /// <summary>The store's own localised price once the product has resolved, or null.</summary>
        /// <remarks>
        /// ⚠ The store's string, never one written here: it carries the player's currency and
        /// their country's formatting, and a price typed into the game would be wrong for most of
        /// the world and stale the day the owner retunes it.
        /// </remarks>
        public string LocalisedPrice { get; private set; }

        /// <summary>
        /// Whether the store has confirmed it actually sells the product. For tests.
        /// </summary>
        /// <remarks>
        /// ⛔ <b>Apple rejected the app under 2.1.0 App Completeness for the want of this</b>
        /// (2026-10-14). A product ID in a settings asset proves only that someone typed a
        /// string; it says nothing about whether the store has anything under that name. The
        /// button was shown on the strength of the string, a reviewer pressed it, and
        /// <see cref="Buy"/> logged "the store has no remove_ads" and did nothing at all.
        /// ⚠ A button that cannot work is not a smaller fault than a crash — it is a feature the
        /// app advertises and does not have.
        /// </remarks>
        public bool ProductAvailable { get; private set; }

        /// <summary>Whether the button should be offered right now.</summary>
        /// <remarks>
        /// <para>
        /// ⓘ <b>Android:</b> three things must hold: a product is named, the STORE AGREES it
        /// exists, and it has not already been bought. ⚠ The middle one arrives asynchronously,
        /// so the button appears a moment after the menu does.
        /// </para>
        /// <para>
        /// ⛔ <b>iOS: the store's answer is not waited for (5.6 rejection, 2026-10-24).</b> A
        /// button that appears only once StoreKit answers was invisible on the reviewer's device
        /// and read as a feature hidden from review. See
        /// <see cref="RemoveAdsRule.ShowTheButton(bool, bool, bool, bool)"/>.
        /// </para>
        /// </remarks>
        public bool ShouldOffer =>
            RemoveAdsRule.ShowTheButton(IsConfigured, ProductAvailable, Purchased, OnApplePlatform);

        /// <summary>Lets a fixture play as an iPhone. Null means "what this really is".</summary>
        public static bool? ApplePlatformForTests { get; set; }

        /// <summary>
        /// A real iPhone or iPad. ⓘ Never the Editor, even on a Mac, so every existing fixture
        /// keeps testing the Android rule unless it asks otherwise.
        /// </summary>
        private static bool OnApplePlatform =>
            ApplePlatformForTests ?? Application.platform == RuntimePlatform.IPhonePlayer;

        /// <summary>
        /// Lets a test say the store stocks the product, as a real store would have.
        /// </summary>
        /// <remarks>
        /// <para>
        /// ⚠ <b>Needed because Play Mode has no store at all.</b> Every fixture here was written
        /// when the button appeared on the strength of a configured id, so they all assert it is
        /// offered. They are still asking the right question — "given something to sell, does the
        /// menu offer it?" — they simply had no way to grant the premise.
        /// </para>
        /// <para>
        /// ⛔ This cannot fake a purchase. <see cref="Buy"/> still asks the real store for the
        /// real product and still does nothing without one; only the button's visibility moves.
        /// </para>
        /// </remarks>
        public void SetProductAvailableForTests(bool available)
        {
            ProductAvailable = available;
            Changed?.Invoke();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private async void Start()
        {
            if (!IsConfigured)
            {
                Status = "not configured";
                return;
            }

            if (Purchased)
            {
                // Nothing to sell, but the store is still asked below so a refund or a second
                // device is noticed. ⓘ Never the other way round: a store that cannot be reached
                // must not take the purchase away.
                Status = "bought";
            }

            if (Application.isEditor)
            {
                Status = Purchased ? "bought (editor: store skipped)" : "editor: skipped";
                return;
            }

#if UNITY_WEBGL
            Status = "web: no store";
            await System.Threading.Tasks.Task.CompletedTask;
#else

            if (_connecting)
            {
                return;
            }

            _connecting = true;

            try
            {
                _store = UnityIAPServices.StoreController();
                _store.OnPurchasePending += OnPurchasePending;
                _store.OnPurchaseConfirmed += OnPurchaseConfirmed;
                _store.OnPurchaseFailed += OnPurchaseFailed;
                _store.OnProductsFetched += OnProductsFetched;
                _store.OnProductsFetchFailed += failure => Note("product fetch failed: " + failure);
                _store.OnPurchasesFetched += OnPurchasesFetched;
                _store.OnPurchasesFetchFailed += failure => Note("purchase fetch failed: " + failure);

                Status = "connecting";
                await _store.Connect();

                _store.FetchProducts(new List<ProductDefinition>
                {
                    new ProductDefinition(_settings.RemoveAdsProductId, ProductType.NonConsumable),
                });
            }
            catch (Exception e)
            {
                // ⛔ A store that will not start is not an error the player should ever see.
                Note("unavailable: " + e.Message);
            }
#endif
        }

        /// <summary>Starts the purchase.</summary>
        /// <returns>
        /// True when the store's purchase sheet was asked for. False when it could not be — no
        /// store, no product, or already bought — so the menu can tell the player instead of
        /// doing nothing (2.1.0, 2026-10-14).
        /// </returns>
        public bool Buy()
        {
            if (Purchased)
            {
                Note("already bought");
                return false;
            }

#if UNITY_WEBGL
            Note("no store on the web — nothing bought");
            return false;
#else
            if (_store == null)
            {
                Note("no store — nothing bought");
                return false;
            }

            Product product = _store.GetProducts()
                .FirstOrDefault(p => p.definition.id == _settings.RemoveAdsProductId);

            if (product == null)
            {
                Note("the store has no " + _settings.RemoveAdsProductId + " — asking again");

                // ⓘ The press is also a retry: a product that failed to resolve at launch (a
                // slow network, a sandbox that woke up late) gets another chance, and the price
                // appears on the button when it arrives.
                if (IsConfigured)
                {
                    _store.FetchProducts(new List<ProductDefinition>
                    {
                        new ProductDefinition(_settings.RemoveAdsProductId, ProductType.NonConsumable),
                    });
                }

                return false;
            }

            Status = "buying";
            _store.PurchaseProduct(product);
            return true;
#endif
        }

        /// <summary>Asks the store for anything this account already owns.</summary>
        public void Restore()
        {
#if UNITY_WEBGL
            Note("no store on the web — nothing to restore");
#else
            if (_store == null)
            {
                Note("no store — nothing to restore");
                return;
            }

            _store.RestoreTransactions((success, error) =>
                Note(success ? "restore finished" : "restore failed: " + error));
#endif
        }

#if !UNITY_WEBGL
        private void OnProductsFetched(List<Product> products)
        {
            Product mine = products.FirstOrDefault(p => p.definition.id == _settings.RemoveAdsProductId);

            if (mine != null)
            {
                LocalisedPrice = mine.metadata.localizedPriceString;

                // ⭐ The only place this becomes true: the store itself handed the product back.
                ProductAvailable = true;

                Note("ready: " + LocalisedPrice);
                Changed?.Invoke();
            }
            else
            {
                // ⛔ No button. The product id is configured but the store has nothing under it
                // -- unfinished Play Console or App Store Connect setup, most often an agreement
                // that is not yet active. Offering it anyway is what got the app rejected.
                ProductAvailable = false;
                Note("ready, but the product is not in the store — the button stays hidden");
                Changed?.Invoke();
            }

            // Ownership is asked for on every launch, so a reinstall restores it silently.
            _store.FetchPurchases();
        }

        private void OnPurchasesFetched(Orders orders)
        {
            if (orders.ConfirmedOrders.Any(Mine))
            {
                Grant("restored");
            }
        }

        private void OnPurchasePending(PendingOrder order)
        {
            // ⛔ The entitlement is granted BEFORE the order is confirmed, which is the order the
            // package's own sample uses: a confirmation that never arrives must not cost the
            // player what they paid for.
            if (Mine(order))
            {
                Grant("bought");
            }

            _store.ConfirmPurchase(order);
        }

        private void OnPurchaseConfirmed(Order order)
        {
            switch (order)
            {
                case FailedOrder failed:
                    Note("confirmation failed: " + failed.FailureReason);
                    break;
                default:
                    if (Mine(order))
                    {
                        Grant("confirmed");
                    }

                    break;
            }
        }

        private void OnPurchaseFailed(FailedOrder failed)
        {
            // A cancelled purchase is the player changing their mind, not a fault.
            Note("purchase failed: " + failed.FailureReason);
        }

        private bool Mine(Order order) =>
            order?.CartOrdered != null &&
            order.CartOrdered.Items().Any(item => item.Product?.definition?.id == _settings.RemoveAdsProductId);

#endif

        private void Grant(string how)
        {
            bool already = Purchased;
            PurchasedState.RememberThePurchase();
            Status = "bought";
            Debug.Log("[IAP] Remove Ads " + how);

            if (already)
            {
                return;
            }

            // Tear the advertising down now rather than at the next scene load.
            AdService.Instance?.Refresh();
            UnityEngine.Object.FindFirstObjectByType<MenuBanner>()?.Refresh();
            Changed?.Invoke();
        }

        private void Note(string status)
        {
            Status = status;
            Debug.Log("[IAP] " + status);
        }
    }
}
