using System;
using System.Threading.Tasks;
using Windows.Services.Store;
using Windows.System;

namespace ImageConverterAT.Services;

public static class StoreUpdateService
{
    public const string StorePackageFamilyName = "49536HowonLee.ATImageConverter_q278kdbtfr3f2";
    public const string StoreProductIdentifier = "9NSKNC3J8GPD";

    private static readonly Uri s_storePackageFamilyNameProductPageAddress = new($"ms-windows-store://pdp/?PFN={StorePackageFamilyName}");
    private static readonly Uri s_storeProductIdentifierProductPageAddress = new($"ms-windows-store://pdp/?productid={StoreProductIdentifier}");

    public static async Task<int> GetAvailableUpdateCountAsync()
    {
        var storeContext = StoreContext.GetDefault();
        var storePackageUpdates = await storeContext.GetAppAndOptionalStorePackageUpdatesAsync();
        return storePackageUpdates.Count;
    }

    public static async Task<bool> OpenStoreProductPageAsync() => await Launcher.LaunchUriAsync(s_storePackageFamilyNameProductPageAddress)
        || await Launcher.LaunchUriAsync(s_storeProductIdentifierProductPageAddress);
}
