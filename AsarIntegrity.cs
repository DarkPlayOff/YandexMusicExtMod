using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace YandexMusicPatcherGui;

public interface IAsarIntegrityPatcher
{
    Task<bool> BypassAsarIntegrity();
}

public static class AsarIntegrity
{
    public static IAsarIntegrityPatcher CreatePatcher()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return new WindowsAsarPatcher();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return new MacAsarPatcher();

        throw new PlatformNotSupportedException("ASAR integrity patching is only supported on Windows and macOS.");
    }
}

internal abstract class AsarPatcherBase
{
    private const int HeaderOffset = 16;

    public async Task<string?> CalculateAsarHeaderHash(string archivePath)
    {
        try
        {
            await using var fileStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read,
                FileShare.Read, 4096, FileOptions.SequentialScan);

            if (fileStream.Length <= HeaderOffset)
                return null;

            fileStream.Position = HeaderOffset;

            var estimatedSize = Math.Min((int)(fileStream.Length - HeaderOffset), 1024 * 1024);
            var buffer = ArrayPool<byte>.Shared.Rent(estimatedSize);

            try
            {
                var totalRead = 0;
                var jsonEndIndex = -1;

                while (totalRead < estimatedSize && jsonEndIndex == -1)
                {
                    var bytesRead = await fileStream.ReadAsync(
                        buffer.AsMemory(totalRead, Math.Min(65536, estimatedSize - totalRead))
                    );

                    if (bytesRead == 0) break;

                    totalRead += bytesRead;
                    jsonEndIndex = FindJsonEnd(buffer.AsSpan(0, totalRead));
                }

                if (jsonEndIndex == -1)
                    return null;

                using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                sha256.AppendData(buffer, 0, jsonEndIndex + 1);
                var hash = sha256.GetHashAndReset();

                return Convert.ToHexString(hash).ToLowerInvariant();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        catch
        {
            return null;
        }
    }

    private static int FindJsonEnd(ReadOnlySpan<byte> data)
    {
        var depth = 0;
        var inString = false;
        var isEscaped = false;

        for (var i = 0; i < data.Length; i++)
        {
            var c = (char)data[i];

            if (isEscaped)
            {
                isEscaped = false;
                continue;
            }

            switch (c)
            {
                case '\\':
                    isEscaped = true;
                    break;
                case '"':
                    inString = !inString;
                    break;
                case '{' when !inString:
                    depth++;
                    break;
                case '}' when !inString:
                    depth--;
                    if (depth == 0) return i;
                    break;
            }
        }

        return -1;
    }
}

internal class WindowsAsarPatcher : AsarPatcherBase, IAsarIntegrityPatcher
{
    private const string ExeFilename = "Яндекс Музыка.exe";
    private const string ResourcesFoldername = "resources";
    private const string OriginalAsarFilename = "oldapp.asar";
    private const string ModifiedAsarFilename = "app.asar";

    public async Task<bool> BypassAsarIntegrity()
    {
        try
        {
            var yandexMusicDirectory = Program.ModPath;
            var exePath = Path.Combine(yandexMusicDirectory, ExeFilename);
            var resourcesPath = Path.Combine(yandexMusicDirectory, ResourcesFoldername);
            var originalAsarPath = Path.Combine(resourcesPath, OriginalAsarFilename);
            var modifiedAsarPath = Path.Combine(resourcesPath, ModifiedAsarFilename);

            var originalHash = await CalculateAsarHeaderHash(originalAsarPath).ConfigureAwait(false);

            var modifiedHash = await CalculateAsarHeaderHash(modifiedAsarPath).ConfigureAwait(false);

            if (string.IsNullOrEmpty(originalHash) || string.IsNullOrEmpty(modifiedHash))
                return false;

            if (originalHash == modifiedHash)
            {
                return true;
            }

            return await PatchExeFile(exePath, originalHash, modifiedHash).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task<bool> PatchExeFile(string exePath, string originalHash, string modifiedHash)
    {
        var originalHashBytes = Encoding.ASCII.GetBytes(originalHash);
        var modifiedHashBytes = Encoding.ASCII.GetBytes(modifiedHash);
        
        var fileBytes = await File.ReadAllBytesAsync(exePath);
        var span = fileBytes.AsSpan();
        
        var offset = span.IndexOf(originalHashBytes);
        if (offset != -1)
        {
            modifiedHashBytes.CopyTo(span.Slice(offset));
            await File.WriteAllBytesAsync(exePath, fileBytes);
            return true;
        }
        
        return span.IndexOf(modifiedHashBytes) != -1;
    }
}

internal class MacAsarPatcher : AsarPatcherBase, IAsarIntegrityPatcher
{
    private readonly string _extractedEntitlementsPath;
    private readonly string _infoPlistPath;
    private readonly string _tmpPath;
    private readonly string _ymAsarPath;
    private readonly string _ymPath;

    public MacAsarPatcher()
    {
        _ymPath = Path.Combine("/Applications", "Яндекс Музыка.app");
        _infoPlistPath = Path.Combine(_ymPath, "Contents", "Info.plist");
        _ymAsarPath = Path.Combine(_ymPath, "Contents", "Resources", "app.asar");

        _tmpPath = Path.Combine(Path.GetTempPath(), "ym-patcher");
        _extractedEntitlementsPath = Path.Combine(_tmpPath, "entitlements.plist");

        Directory.CreateDirectory(_tmpPath);
    }

    public async Task<bool> BypassAsarIntegrity()
    {
        try
        {
            await BypassAsarIntegrityDarwin();

            await ReplaceSignDarwin();

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    private async Task BypassAsarIntegrityDarwin()
    {
        var newHash = await CalculateAsarHeaderHash(_ymAsarPath).ConfigureAwait(false);
        if (string.IsNullOrEmpty(newHash))
            throw new InvalidOperationException("Failed to calculate new ASAR hash.");

        var doc = XDocument.Load(_infoPlistPath);

        var integrityDict = doc.Root?.Element("dict")?
            .Elements("key")
            .FirstOrDefault(k => k.Value == "ElectronAsarIntegrity")?
            .NextNode as XElement;

        var asarDict = integrityDict?
            .Elements("key")
            .FirstOrDefault(k => k.Value == "Resources/app.asar")?
            .NextNode as XElement;

        var hashValueElement = asarDict?
            .Elements("key")
            .FirstOrDefault(k => k.Value == "hash")?
            .NextNode as XElement;

        if (hashValueElement != null && hashValueElement.Name.LocalName == "string")
        {
            hashValueElement.Value = newHash;
            doc.Save(_infoPlistPath);
        }
    }

    private async Task ReplaceSignDarwin()
    {
        try
        {
            await Patcher.RunProcess("/bin/bash", $"-c \"codesign -d --entitlements :- '{_ymPath}' > '{_extractedEntitlementsPath}'\"", "извлечения разрешений");
        }
        catch (Exception)
        {
        }

        await Patcher.RunProcess("/bin/bash", $"-c \"codesign --force --entitlements '{_extractedEntitlementsPath}' --sign - '{_ymPath}'\"", "замены подписи");
    }
}