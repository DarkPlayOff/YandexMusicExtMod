using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.IO.MemoryMappedFiles;
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
    private const int ChunkSize = 4 * 1024 * 1024;

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

            var result = await PatchExeFileMemoryMapped(exePath, originalHash, modifiedHash).ConfigureAwait(false);
            return result;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task<bool> PatchExeFileMemoryMapped(string exePath, string originalHash, string modifiedHash)
    {
        var originalHashBytes = Encoding.ASCII.GetBytes(originalHash);
        var modifiedHashBytes = Encoding.ASCII.GetBytes(modifiedHash);

        return await Task.Run(() =>
        {
            using var fileStream = new FileStream(exePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            using var mmf = MemoryMappedFile.CreateFromFile(fileStream, null, fileStream.Length,
                MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false);

            var fileLength = fileStream.Length;
            var searchResult = ParallelSearchInMemoryMappedFile(mmf, fileLength, originalHashBytes, modifiedHashBytes);

            if (searchResult.Found)
            {
                if (searchResult.IsOriginalHash)
                {
                    using var accessor = mmf.CreateViewAccessor(searchResult.Offset, modifiedHashBytes.Length);
                    accessor.WriteArray(0, modifiedHashBytes, 0, modifiedHashBytes.Length);
                }

                return true;
            }

            return false;
        });
    }

    private static (bool Found, bool IsOriginalHash, long Offset) ParallelSearchInMemoryMappedFile(
        MemoryMappedFile mmf, long fileLength, byte[] originalHashBytes, byte[] modifiedHashBytes)
    {
        var chunks = (int)Math.Ceiling((double)fileLength / ChunkSize);
        var overlap = Math.Max(originalHashBytes.Length, modifiedHashBytes.Length);

        var result = (Found: false, IsOriginalHash: false, Offset: 0L);
        var resultLock = new object();

        Parallel.For(0, chunks, (chunkIndex, state) =>
        {
            var startOffset = chunkIndex * (long)ChunkSize;
            var chunkSize = (int)Math.Min(ChunkSize + overlap, fileLength - startOffset);

            if (chunkSize <= 0) return;

            try
            {
                using var accessor = mmf.CreateViewAccessor(startOffset, chunkSize);
                var buffer = ArrayPool<byte>.Shared.Rent(chunkSize);
                try
                {
                    accessor.ReadArray(0, buffer, 0, chunkSize);
                    var span = buffer.AsSpan(0, chunkSize);

                    var offset = span.IndexOf(originalHashBytes);
                    if (offset != -1)
                    {
                        lock (resultLock)
                        {
                            result = (true, true, startOffset + offset);
                        }

                        state.Stop();
                        return;
                    }

                    offset = span.IndexOf(modifiedHashBytes);
                    if (offset != -1)
                    {
                        lock (resultLock)
                        {
                            result = (true, false, startOffset + offset);
                        }

                        state.Stop();
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            catch
            {
                /* Ignore errors in parallel search */
            }
        });

        return result;
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

            ReplaceSignDarwin();

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task BypassAsarIntegrityDarwin()
    {
        var newHash = await CalculateAsarHeaderHash(_ymAsarPath);
        if (newHash == null) throw new InvalidOperationException("Failed to calculate new ASAR hash.");

        var plistData = PlistParser.Parse(await File.ReadAllTextAsync(_infoPlistPath));
        if (plistData["ElectronAsarIntegrity"] is Dictionary<string, object> integrity &&
            integrity["Resources/app.asar"] is Dictionary<string, object> resource)
            resource["hash"] = newHash;

        await File.WriteAllTextAsync(_infoPlistPath, PlistParser.Build(plistData));
    }

    private void ReplaceSignDarwin()
    {
        try
        {
            ExecuteCommand($"codesign -d --entitlements :- '{_ymPath}' > '{_extractedEntitlementsPath}'");
        }
        catch (Exception)
        {
        }

        ExecuteCommand($"codesign --force --entitlements '{_extractedEntitlementsPath}' --sign - '{_ymPath}'");
    }


    private static void ExecuteCommand(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{command}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) return;

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            throw new Exception($"Command failed with exit code {process.ExitCode}: {error}");
        }
    }

    private static class PlistParser
    {
        public static Dictionary<string, object> Parse(string plistContent)
        {
            var doc = XDocument.Parse(plistContent);
            var rootDict = doc.Element("plist")?.Element("dict");
            return rootDict != null ? ParseDict(rootDict) : new Dictionary<string, object>();
        }

        public static string Build(Dictionary<string, object> data)
        {
            var doc = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN",
                    "https://www.apple.com/DTDs/PropertyList-1.0.dtd", null),
                new XElement("plist", new XAttribute("version", "1.0"), BuildDict(data))
            );
            return doc.Declaration + Environment.NewLine + doc;
        }

        private static Dictionary<string, object> ParseDict(XElement dictElement)
        {
            var dict = new Dictionary<string, object>();
            var elements = dictElement.Elements().ToList();

            for (var i = 0; i < elements.Count - 1; i += 2)
                if (elements[i].Name.LocalName == "key")
                    dict[elements[i].Value] = ParseValue(elements[i + 1]);
            return dict;
        }

        private static object ParseValue(XElement element)
        {
            return element.Name.LocalName switch
            {
                "string" => element.Value,
                "integer" => long.Parse(element.Value),
                "real" => double.Parse(element.Value, CultureInfo.InvariantCulture),
                "true" => true,
                "false" => false,
                "date" => DateTime.Parse(element.Value, null, DateTimeStyles.RoundtripKind),
                "data" => Convert.FromBase64String(element.Value),
                "array" => element.Elements().Select(ParseValue).ToList(),
                "dict" => ParseDict(element),
                _ => element.Value
            };
        }

        private static XElement BuildValue(object value)
        {
            return value switch
            {
                string s => new XElement("string", s),
                int or long => new XElement("integer", value),
                float or double => new XElement("real",
                    ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture)),
                bool b => new XElement(b ? "true" : "false"),
                DateTime dt => new XElement("date", dt.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'")),
                byte[] data => new XElement("data", Convert.ToBase64String(data)),
                Dictionary<string, object> dict => BuildDict(dict),
                IList<object> list => new XElement("array", list.Select(BuildValue)),
                _ => new XElement("string", value?.ToString() ?? "")
            };
        }

        private static XElement BuildDict(Dictionary<string, object> dict)
        {
            var elements = new List<XElement>();
            foreach (var kvp in dict)
            {
                elements.Add(new XElement("key", kvp.Key));
                elements.Add(BuildValue(kvp.Value));
            }

            return new XElement("dict", elements);
        }
    }
}