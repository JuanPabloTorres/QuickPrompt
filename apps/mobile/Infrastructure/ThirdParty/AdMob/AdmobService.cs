using System;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using Plugin.MauiMTAdmob;
using QuickPrompt.Models;

namespace QuickPrompt.Services
{
    public class AdmobService
    {
        private string _interstitialAdId; // Reemplázalo con tu ID real

        public AdmobService(IOptions<AdMobSettings> adMobSettings)
        {
            _interstitialAdId = adMobSettings.Value.InterstitialAdId;

            LoadInterstitialAd(); // Precargar el anuncio
        }

        /// <summary>
        /// Carga un anuncio Interstitial.
        /// </summary>
        public void LoadInterstitialAd()
        {
        
            CrossMauiMTAdmob.Current.LoadInterstitial(_interstitialAdId);
        }

        /// <summary>
        /// Muestra el anuncio Interstitial si está cargado.
        /// </summary>
        public async Task ShowInterstitialAdAsync()
        {
            if (CrossMauiMTAdmob.Current.IsInterstitialLoaded())
            {
                CrossMauiMTAdmob.Current.ShowInterstitial();

                LoadInterstitialAd(); // Cargar otro anuncio después de mostrarlo
            }
        }

        /// <summary>
        /// Configura eventos de los anuncios interstitial con logging.
        /// </summary>
        public void SetupAdEvents()
        {
            CrossMauiMTAdmob.Current.OnInterstitialLoaded += (sender, args) =>
            {
                Debug.WriteLine("[AdMob] Interstitial ad loaded successfully");
            };

            CrossMauiMTAdmob.Current.OnInterstitialFailedToLoad += (sender, args) =>
            {
                Debug.WriteLine($"[AdMob] Failed to load interstitial ad: {args}");
            };

            CrossMauiMTAdmob.Current.OnInterstitialOpened += (sender, args) =>
            {
                Debug.WriteLine("[AdMob] Interstitial ad opened");
            };

            CrossMauiMTAdmob.Current.OnInterstitialClosed += (sender, args) =>
            {
                Debug.WriteLine("[AdMob] Interstitial ad closed, preloading next ad");
                LoadInterstitialAd();
            };

            CrossMauiMTAdmob.Current.OnInterstitialFailedToShow += (sender, args) =>
            {
                Debug.WriteLine($"[AdMob] Failed to show interstitial ad: {args}");
            };
        }

        public async Task ShowInterstitialAdAndWaitAsync()
        {
            if (!CrossMauiMTAdmob.Current.IsInterstitialLoaded())
            {
                Debug.WriteLine("[AdMob] Interstitial ad not loaded, skipping");
                return;
            }

            var tcs = new TaskCompletionSource<bool>();

            EventHandler handler = null!;
            handler = (s, e) =>
            {
                CrossMauiMTAdmob.Current.OnInterstitialClosed -= handler;
                tcs.TrySetResult(true);
            };

            CrossMauiMTAdmob.Current.OnInterstitialClosed += handler;

            CrossMauiMTAdmob.Current.ShowInterstitial();

            LoadInterstitialAd();

            await tcs.Task;
        }


    }
}