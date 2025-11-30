using System.IO.Compression;
using ZstdSharp;

namespace YandexMusicPatcherGui.Services;

public class ArchLinuxService : BasePlatformService
{
    private const string AppInstallPath = "/opt/Яндекс Музыка";
    private const string AppExecutableName = "Яндекс Музыка";
    private const string DebName = "Yandex_Music.deb";

    private string _buildDir = string.Empty;
    private string _pkgbuildDir = string.Empty;
    private string _debPath = string.Empty;

    public override string GetModPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".local", "share", "YandexMusicPatcherGui");
    }

    public override string GetAsarPath()
    {
        return Path.Combine(AppInstallPath, "resources", "app.asar");
    }

    public override async Task DownloadLatestMusic(string tempFolder)
    {
        const string latestUrl = "https://music-desktop-application.s3.yandex.net/stable/Yandex_Music.deb";
        _debPath = Path.Combine(tempFolder, DebName);
        await Patcher.DownloadFileWithProgress(latestUrl, _debPath, "Загрузка клиента");

        Patcher.ReportProgress(40, "Подготовка к сборке...");

        _buildDir = Path.Combine(tempFolder, "build");
        if (Directory.Exists(_buildDir)) Directory.Delete(_buildDir, true);
        Directory.CreateDirectory(_buildDir);
        
        _pkgbuildDir = Path.Combine(tempFolder, "pkgbuild");
        if (Directory.Exists(_pkgbuildDir)) Directory.Delete(_pkgbuildDir, true);
        Directory.CreateDirectory(_pkgbuildDir);

        var tempDebExtractDir = Path.Combine(tempFolder, "deb-extract");
        if (Directory.Exists(tempDebExtractDir)) Directory.Delete(tempDebExtractDir, true);
        Directory.CreateDirectory(tempDebExtractDir);
        
        await Patcher.RunProcess("ar", $"x \"{_debPath}\"", "Распаковка deb архива", tempDebExtractDir);
        
        var dataArchive = Directory.GetFiles(tempDebExtractDir, "data.tar.*").FirstOrDefault() 
                          ?? throw new FileNotFoundException("Не удалось найти data.tar.* в deb пакете.");
        
        await Patcher.RunProcess("tar", $"-xf \"{dataArchive}\" -C \"{_buildDir}\"", "Распаковка данных");
    }
    
    public override async Task InstallMod(string archivePath, string tempFolder)
    {
        var asarPath = Path.Combine(_buildDir, "opt", "Яндекс Музыка", "resources", "app.asar");
        var asarDir = Path.GetDirectoryName(asarPath)!;
        if (!Directory.Exists(asarDir))
        {
            Directory.CreateDirectory(asarDir);
        }
        
        await using var sourceStream = File.OpenRead(archivePath);
        await using var destinationStream = File.Create(asarPath);
        await using var decompressionStream = new DecompressionStream(sourceStream);
        await decompressionStream.CopyToAsync(destinationStream);
    }

    public override Task InstallModUnpacked(string archivePath, string tempFolder)
    {
         return Task.CompletedTask;
    }
    
    public override async Task FinishInstallation(string tempFolder)
    {
        Patcher.ReportProgress(50, "Подготовка PKGBUILD...");
        
        var inspectDir = Path.Combine(tempFolder, "inspect");
        if (Directory.Exists(inspectDir)) Directory.Delete(inspectDir, true);
        Directory.CreateDirectory(inspectDir);

        await Patcher.RunProcess("ar", $"x \"{_debPath}\"", "Чтение метаданных...", inspectDir);
        var controlArchive = Directory.GetFiles(inspectDir, "control.tar.*").FirstOrDefault();
        if (controlArchive != null)
        {
            await Patcher.RunProcess("tar", $"-xf \"{Path.GetFileName(controlArchive)}\"", "Распаковка control архива...", inspectDir);
        }

        var controlFilePath = Path.Combine(inspectDir, "control");
        var pkgVer = "1.0.0";
        var pkgArch = "x86_64";
        var pkgDesc = "Yandex Music";

        if (File.Exists(controlFilePath))
        {
            var controlLines = await File.ReadAllLinesAsync(controlFilePath);
            
            pkgVer = controlLines.FirstOrDefault(l => l.StartsWith("Version:"))?.Split(':', 2).ElementAtOrDefault(1)?.Trim() ?? pkgVer;
            pkgArch = controlLines.FirstOrDefault(l => l.StartsWith("Architecture:"))?.Split(':', 2).ElementAtOrDefault(1)?.Trim() ?? pkgArch;
            pkgDesc = controlLines.FirstOrDefault(l => l.StartsWith("Description:"))?.Split(':', 2).ElementAtOrDefault(1)?.Trim() ?? pkgDesc;

        }

        if (pkgArch == "amd64") pkgArch = "x86_64";

        var pkgRel = "1";
        if (pkgVer.Contains('-'))
        {
            var parts = pkgVer.Split(new[] { '-' }, 2);
            pkgVer = parts[0];
            if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1])) pkgRel = parts[1];
        }

        var pkgbuildContent = $@"# Builded by YandexMusicExtMod
pkgname=yandex-music
_pkgver={pkgVer}
pkgver=${{_pkgver}}
pkgrel=1
pkgdesc=""{pkgDesc}""
arch=('{pkgArch}')
url=""https://github.com/DarkPlayOff/YandexMusicExtMod""
license=('custom')
source=()
sha256sums=()

package() {{
    cp -r ""{_buildDir}/""* ""$pkgdir/""
}}
";
        await File.WriteAllTextAsync(Path.Combine(_pkgbuildDir, "PKGBUILD"), pkgbuildContent.Replace("\r\n", "\n"));

        Patcher.ReportProgress(60, "Сборка пакета...");
        
        var sudoUser = Environment.GetEnvironmentVariable("SUDO_USER") ?? throw new Exception("Не удалось определить пользователя для сборки пакета.");

        await Patcher.RunProcess("chown", $"-R {sudoUser}:{sudoUser} \"{_pkgbuildDir}\"", "Смена владельца");
        await Patcher.RunProcess("chown", $"-R {sudoUser}:{sudoUser} \"{_buildDir}\"", "Смена владельца");
        
        await Patcher.RunProcess("runuser", $"-u {sudoUser} -- makepkg -f --noconfirm", "Сборка пакета", _pkgbuildDir);

        var packageFile = Directory.GetFiles(_pkgbuildDir, "yandex-music-*.pkg.tar.zst").FirstOrDefault() ?? throw new FileNotFoundException("Не удалось найти собранный пакет Arch Linux.");

        Patcher.ReportProgress(90, "Установка пакета...");
        await Patcher.RunProcess("pacman", $"-U --noconfirm \"{packageFile}\"", "Установка пакета");
    }


    public override Task<(bool, string)> IsSupported()
    {
        if (Environment.UserName != "root")
        {
            return Task.FromResult((false, "Для установки требуются права root."));
        }
        return Task.FromResult((true, string.Empty));
    }

    public override Task CreateDesktopShortcut(string linkName, string path)
    {
        return Task.CompletedTask;
    }

    public override void RunApplication()
    {
        Patcher.RunProcess(GetApplicationExecutablePath(), "", "запуска приложения").GetAwaiter().GetResult();
    }

    public override string GetApplicationExecutablePath()
    {
        return Path.Combine(AppInstallPath, AppExecutableName);
    }

}