using Microsoft.AspNetCore.DataProtection;

namespace ItechMarineAPI.Security;

public interface IProtectionService
{
    string ProtectDeviceKey(string plain);
    string UnprotectDeviceKey(string protectedCipher);
}

public class ProtectionService : IProtectionService
{
    private readonly IDataProtector _protector;
    public ProtectionService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("marinecontrol/devicekey/v1");
    }

    public string ProtectDeviceKey(string plain) => _protector.Protect(plain);
    public string UnprotectDeviceKey(string protectedCipher) => _protector.Unprotect(protectedCipher);
}
