using System;

class DeviceInfo
{
    private string _version;

    public DeviceInfo() { }

    public string GetAppVersion()
    {
        if (string.IsNullOrEmpty(_version))
        {
            _version = Windows.ApplicationModel.Package.Current.Id.Version.ToString();
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
        return "Manufacturer";
    }

    public string GetModel()
    {
        return "Model";
    }

    public string GetBrand()
    {
        return "Brand";
    }

    public string GetCarrier()
    {
        return "Carrier";
    }

    public string GetCountry()
    {
        return "Country";
    }

    public string GetLanguage()
    {
        return "Language";
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