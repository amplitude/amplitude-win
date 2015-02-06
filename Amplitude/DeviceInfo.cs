using System;
using Windows.Networking.Connectivity;
using Windows.Security.ExchangeActiveSyncProvisioning;

class DeviceInfo
{
    private string _version;

    public DeviceInfo() { }

    public string GetAppVersion()
    {
        if (string.IsNullOrEmpty(_version))
        {
            var version = Windows.ApplicationModel.Package.Current.Id.Version;
            _version = version.Major + "." + version.Minor;
        }
        return _version;
    }

    public string GetOsName()
    {
        return "Windows Phone";
    }

    public string GetOsVersion()
    {
        return "8.1";
    }

    public string GetManufacturer()
    {
        var deviceInfo = new EasClientDeviceInformation();
        return deviceInfo.SystemManufacturer;
    }

    public string GetModel()
    {
        var deviceInfo = new EasClientDeviceInformation();
        return deviceInfo.SystemProductName;
    }

    public string GetBrand()
    {
        return null;
    }

    public string GetCarrier()
    {
        var result = NetworkInformation.GetConnectionProfiles();
        foreach (var connectionProfile in result)
        {
            if (connectionProfile.IsWwanConnectionProfile)
            {
                foreach (var networkName in connectionProfile.GetNetworkNames())
                {
                    return networkName;
                }
            }
        }
        return null;
    }

    public string GetCountry()
    {
        return Windows.System.UserProfile.GlobalizationPreferences.HomeGeographicRegion;
    }

    public string GetLanguage()
    {
        // For now, remove the region part of the BCP 47 tag
        return Windows.System.UserProfile.GlobalizationPreferences.Languages[0].Split('-')[0];
    }

    public string GetAdvertiserId()
    {
        return null;
    }

    public string GetPlatform()
    {
        return "Windows";
    }
}
