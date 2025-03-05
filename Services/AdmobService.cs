using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Plugin.MauiMTAdmob;
using QuickPrompt.Models;

namespace QuickPrompt.Services
{
    public class AdmobService
    {
        private string _interstitialAdId; // Reemplázalo con tu ID real

        public AdmobService(AdMobSettings adMobSettings)
        {
            _interstitialAdId = adMobSettings.InterstitialAdId;

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
        /// Configura eventos de los anuncios interstitial.
        /// </summary>
        public void SetupAdEvents()
        {
            CrossMauiMTAdmob.Current.OnInterstitialLoaded += (sender, args) =>
            {
                Console.WriteLine("Interstitial Ad Loaded.");
            };

            CrossMauiMTAdmob.Current.OnInterstitialFailedToLoad += (sender, args) =>
            {
                Console.WriteLine($"Interstitial Ad Failed to Load: {args.ErrorMessage}");
            };

            CrossMauiMTAdmob.Current.OnInterstitialOpened += (sender, args) =>
            {
                Console.WriteLine("Interstitial Ad Opened.");
            };

            CrossMauiMTAdmob.Current.OnInterstitialClosed += (sender, args) =>
            {
                Console.WriteLine("Interstitial Ad Closed. Reloading...");
                LoadInterstitialAd();
            };

            CrossMauiMTAdmob.Current.OnInterstitialFailedToShow += (sender, args) =>
            {
                Console.WriteLine($"Interstitial Ad Failed to Show: {args.ErrorMessage}");
            };
        }
    }
}