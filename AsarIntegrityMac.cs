using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using YandexMusicPatcherGui;

namespace YandexMusicPatcher
{
    public delegate void ProgressCallback(double progress, string message, string? error = null);

    public class YMPatcher
    {
        private static readonly Dictionary<string, string> DEFAULT_YM_PATH = new()
        {
            ["darwin"] = Path.Combine("/Applications", "Яндекс Музыка.app"),
        };

        private string YM_PATH, INFO_PLIST_PATH = null!, YM_ASAR_PATH = null!, oldYMHash = null!;
        private readonly string TMP_PATH, ASAR_TMP_BACKUP_PATH, YM_EXE_TMP_BACKUP_PATH, EXTRACTED_ENTITLEMENTS_PATH;
        private readonly string platform;
        private bool IsMac => platform == "darwin";

        public YMPatcher(string? tmpPath = null)
        {
            platform = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "darwin" :
                      RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win32" : "linux";

            TMP_PATH = tmpPath ?? Path.Combine(Path.GetTempPath(), "ym-patcher");
            ASAR_TMP_BACKUP_PATH = Path.Combine(TMP_PATH, "app.asar.backup");
            YM_EXE_TMP_BACKUP_PATH = Path.Combine(TMP_PATH, "YandexMusic.exe.backup");
            EXTRACTED_ENTITLEMENTS_PATH = Path.Combine(TMP_PATH, "entitlements.plist");

            YM_PATH = DEFAULT_YM_PATH[platform];
            UpdatePaths(YM_PATH);
        }

        public async Task Initialize()
        {
            Directory.CreateDirectory(TMP_PATH);
            oldYMHash = (await new HashPatcher().CalculateAsarHeaderHash(YM_ASAR_PATH))!;
        }

        public void UpdatePaths(string ymPath)
        {
            YM_PATH = ymPath;
            INFO_PLIST_PATH = Path.Combine(YM_PATH, "Contents", "Info.plist");
            YM_ASAR_PATH = Path.Combine(YM_PATH,
                platform == "darwin" ? Path.Combine("Contents", "Resources", "app.asar") :
                Path.Combine("resources", "app.asar"));
        }

        public async Task ClearCaches(ProgressCallback callback)
        {
            callback(1, "Clearing caches...");
            await Task.Run(() =>
            {
                foreach (var path in new[] { ASAR_TMP_BACKUP_PATH, YM_EXE_TMP_BACKUP_PATH, EXTRACTED_ENTITLEMENTS_PATH })
                    if (File.Exists(path)) File.Delete(path);
            });
            callback(1, "Caches cleared.");
        }

        public async Task<bool> BypassAsarIntegrity(string appPath, ProgressCallback callback)
        {
            try
            {
                {
                    await BypassAsarIntegrityDarwin(callback);
                    ReplaceSignDarwin(callback, appPath);

                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task BypassAsarIntegrityDarwin(ProgressCallback callback)
        {
            var newHash = (await new HashPatcher().CalculateAsarHeaderHash(YM_ASAR_PATH))!;

            var plistData = PlistParser.Parse(File.ReadAllText(INFO_PLIST_PATH));
            if (plistData["ElectronAsarIntegrity"] is Dictionary<string, object> integrity &&
                integrity["Resources/app.asar"] is Dictionary<string, object> resource)
            {
                resource["hash"] = newHash;
            }
            File.WriteAllText(INFO_PLIST_PATH, PlistParser.Build(plistData));
        }

        private void ReplaceSignDarwin(ProgressCallback callback, string appPath)
        {
            try
            {
                ExecuteCommand($"codesign -d --entitlements :- '{appPath}' > '{EXTRACTED_ENTITLEMENTS_PATH}'");
            }
            catch (Exception)
            {
            }
            ExecuteCommand($"codesign --force --entitlements '{EXTRACTED_ENTITLEMENTS_PATH}' --sign - '{appPath}'");
        }

        private bool CheckIfElectronAsarIntegrityIsUsed() =>
            File.Exists(INFO_PLIST_PATH) && File.ReadAllText(INFO_PLIST_PATH).Contains("ElectronAsarIntegrity");

        private string ExecuteCommand(string command, bool returnOutput = false)
        {
            var psi = new ProcessStartInfo
            {
                FileName = IsMac ? "/bin/bash" : "cmd.exe",
                Arguments = IsMac ? $"-c \"{command}\"" : $"/c {command}",
                UseShellExecute = false,
                RedirectStandardOutput = returnOutput,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null) return string.Empty;
            process.WaitForExit();

            if (returnOutput) return process.StandardOutput.ReadToEnd();
            if (process.ExitCode != 0) throw new Exception($"Command failed: {process.StandardError.ReadToEnd()}");
            return string.Empty;
        }
    }

    public static class PlistParser
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
                new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null),
                new XElement("plist", new XAttribute("version", "1.0"), BuildDict(data))
            );
            return doc.Declaration + Environment.NewLine + doc;
        }

        private static Dictionary<string, object> ParseDict(XElement dictElement)
        {
            var dict = new Dictionary<string, object>();
            var elements = dictElement.Elements().ToList();

            for (int i = 0; i < elements.Count - 1; i += 2)
            {
                if (elements[i].Name.LocalName == "key")
                    dict[elements[i].Value] = ParseValue(elements[i + 1]);
            }
            return dict;
        }

        private static object ParseValue(XElement element) => element.Name.LocalName switch
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

        private static XElement BuildValue(object value) => value switch
        {
            string s => new XElement("string", s),
            int or long => new XElement("integer", value),
            float or double => new XElement("real", ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture)),
            bool b => new XElement(b ? "true" : "false"),
            DateTime dt => new XElement("date", dt.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'")),
            byte[] data => new XElement("data", Convert.ToBase64String(data)),
            Dictionary<string, object> dict => BuildDict(dict),
            IList<object> list => new XElement("array", list.Select(BuildValue)),
            _ => new XElement("string", value?.ToString() ?? "")
        };

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