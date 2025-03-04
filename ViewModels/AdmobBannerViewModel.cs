using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.ViewModels
{
    public partial class AdmobBannerViewModel : BaseViewModel
    {
        [ObservableProperty]
        private string adUnitId;

        public AdmobBannerViewModel()
        {
            SetAdUnitId();
        }

        /// <summary>
        /// Configura el ID de los anuncios de AdMob según la plataforma.
        /// </summary>
        private void SetAdUnitId()
        {
#if __ANDROID__
            AdUnitId = "ca-app-pub-6397442763590886/6154534752"; // Reemplaza con tu ID real de AdMob
#elif __IOS__
            AdUnitId = "ca-app-pub-6397442763590886/6300978111"; // Reemplaza con tu ID real de AdMob
#endif
        }
    }
}
