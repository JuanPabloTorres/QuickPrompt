using CommunityToolkit.Mvvm.ComponentModel;
using QuickPrompt.Models;
using QuickPrompt.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public partial class AdmobBannerViewModel : BaseViewModel
{
    private readonly AdMobSettings _adMobSettings;

    /// <summary>
    /// ID del anuncio de AdMob según la plataforma.
    /// </summary>
    public string AdUnitId { get; private set; }

    public AdmobBannerViewModel(AdMobSettings adMobSettings)
    {
        _adMobSettings = adMobSettings ?? throw new ArgumentNullException(nameof(adMobSettings));

        AdUnitId = GetAdUnitId();
    }

    /// <summary>
    /// Obtiene el ID del anuncio de AdMob según la plataforma.
    /// </summary>
    /// <returns>
    /// ID de AdMob configurado para la plataforma actual.
    /// </returns>
    private string GetAdUnitId()
    {
        if (_adMobSettings == null)
            throw new InvalidOperationException("Los ajustes de AdMob no están configurados.");

        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            return _adMobSettings.Android ?? throw new InvalidOperationException("AdMob ID para Android no está configurado.");
        }
        else if (DeviceInfo.Platform == DevicePlatform.iOS)
        {
            return _adMobSettings.iOs ?? throw new InvalidOperationException("AdMob ID para iOS no está configurado.");
        }
        else
        {
            throw new PlatformNotSupportedException("Plataforma no soportada para anuncios AdMob.");
        }
    }
}