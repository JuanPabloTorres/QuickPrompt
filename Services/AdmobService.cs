using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        /// Configura eventos de los anuncios interstitial.
        /// </summary>
        public void SetupAdEvents()
        {
            CrossMauiMTAdmob.Current.OnInterstitialLoaded += (sender, args) =>
            {
            };

            CrossMauiMTAdmob.Current.OnInterstitialFailedToLoad += (sender, args) =>
            {
            };

            CrossMauiMTAdmob.Current.OnInterstitialOpened += (sender, args) =>
            {
            };

            CrossMauiMTAdmob.Current.OnInterstitialClosed += (sender, args) =>
            {
                LoadInterstitialAd();
            };

            CrossMauiMTAdmob.Current.OnInterstitialFailedToShow += (sender, args) =>
            {
            };
        }
    }
}