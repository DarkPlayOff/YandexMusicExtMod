using System.IO.Compression;

namespace YandexMusicPatcherGui.Services;

public abstract class BasePlatformService : IPlatformService
{
    public abstract string GetModPath();
    public abstract string GetAsarPath();
    public abstract Task DownloadLatestMusic(string tempFolder, CancellationToken cancellationToken);
    public abstract Task<(bool, string)> IsSupported();
    public abstract Task CreateDesktopShortcut(string linkName, string path);
    public abstract void RunApplication();
    public abstract string GetPatchMessage();
    public abstract string GetApplicationExecutablePath();

    public async Task InstallMod(string archivePath, string tempFolder, CancellationToken cancellationToken)
    {
        var asarPath = GetAsarPath();
        await using var sourceStream = File.OpenRead(archivePath);
        await using var destinationStream = File.Create(asarPath);
        await using var decompressionStream = new ZstdSharp.DecompressionStream(sourceStream);
        await decompressionStream.CopyToAsync(destinationStream, cancellationToken);
    }

    public Task InstallModUnpacked(string archivePath, string tempFolder, CancellationToken cancellationToken)
    {
        var asarPath = GetAsarPath();
        var resourcesPath = Path.GetDirectoryName(asarPath)!;
        var unpackedAsarPath = Path.Combine(resourcesPath, "app.asar.unpacked");

        if (Directory.Exists(unpackedAsarPath))
        {
            Directory.Delete(unpackedAsarPath, true);
        }

        ZipFile.ExtractToDirectory(archivePath, unpackedAsarPath);
        return Task.CompletedTask;
    }
}