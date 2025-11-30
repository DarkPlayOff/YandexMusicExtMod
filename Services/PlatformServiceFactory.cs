using System.Runtime.InteropServices;

namespace YandexMusicPatcherGui.Services;

public static class PlatformServiceFactory
{
    public static IPlatformService Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsService();
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new MacOsService();
        }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return IsArchBased() ? new ArchLinuxService() : new LinuxService();
                }
                throw new PlatformNotSupportedException();
            }
        
                private static bool IsArchBased()
                {
                    return File.Exists("/usr/bin/pacman");
                }
        }