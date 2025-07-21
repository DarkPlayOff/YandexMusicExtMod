using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace YandexMusicPatcherGui;

    

    public class HashPatcher
{
    private const string EXE_FILENAME = "Яндекс Музыка.exe";
    private const string RESOURCES_FOLDERNAME = "resources";
    private const string ORIGINAL_ASAR_FILENAME = "oldapp.asar";
    private const string MODIFIED_ASAR_FILENAME = "app.asar";

    public async Task<bool> PatchExecutable(string yandexMusicDirectory)
    {
        try
        {

            string exePath = Path.Combine(yandexMusicDirectory, EXE_FILENAME);
            string resourcesPath = Path.Combine(yandexMusicDirectory, RESOURCES_FOLDERNAME);
            string originalAsarPath = Path.Combine(resourcesPath, ORIGINAL_ASAR_FILENAME);
            string modifiedAsarPath = Path.Combine(resourcesPath, MODIFIED_ASAR_FILENAME);

            Console.WriteLine($"\nПуть к exe: {exePath}");
            Console.WriteLine($"Путь к оригинальному asar: {originalAsarPath}");
            Console.WriteLine($"Путь к модифицированному asar: {modifiedAsarPath}");

            // 3) Вычисляем хэши
            string originalHash = await CalculateAsarHeaderHash(originalAsarPath);
            if (string.IsNullOrEmpty(originalHash)) return false;

            string modifiedHash = await CalculateAsarHeaderHash(modifiedAsarPath);
            if (string.IsNullOrEmpty(modifiedHash)) return false;

            if (originalHash == modifiedHash)
            {
                Console.WriteLine("\n[ИНФО] Хэши оригинального и модифицированного файлов совпадают. Патчинг не требуется.");
                return true;
            }

            // 4) Читаем exe файл и ищем место для патча
            Console.WriteLine("\nЧтение exe файла...");
            byte[] exeData = await File.ReadAllBytesAsync(exePath);

            byte[] originalHashBytes = Encoding.ASCII.GetBytes(originalHash);
            byte[] modifiedHashBytes = Encoding.ASCII.GetBytes(modifiedHash);

            Console.WriteLine($"Поиск хэша '{originalHash}' в файле...");
            int hashOffset = FindBytes(exeData, originalHashBytes);

            if (hashOffset == -1)
            {
                int alreadyPatchedOffset = FindBytes(exeData, modifiedHashBytes);
                if (alreadyPatchedOffset != -1)
                {
                    return true;
                }

                return false;
            }

            Array.Copy(modifiedHashBytes, 0, exeData, hashOffset, modifiedHashBytes.Length);

            await File.WriteAllBytesAsync(exePath, exeData);

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    private async Task<string> CalculateAsarHeaderHash(string archivePath)
    {
        try
        {
            byte[] fileBytes = await File.ReadAllBytesAsync(archivePath);
            const int headerOffset = 16;
            if (fileBytes.Length <= headerOffset)
            {
                return null;
            }

            byte[] headerData = new byte[fileBytes.Length - headerOffset];
            Array.Copy(fileBytes, headerOffset, headerData, 0, headerData.Length);

            string jsonStr = Encoding.UTF8.GetString(headerData);
            int jsonEndIndex = FindJsonEnd(jsonStr);

            if (jsonEndIndex == -1)
            {
                return null;
            }

            byte[] jsonBytes = new byte[jsonEndIndex + 1];
            Array.Copy(headerData, 0, jsonBytes, 0, jsonBytes.Length);

            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(jsonBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    private int FindJsonEnd(string json)
    {
        int depth = 0;
        bool inString = false;
        bool isEscaped = false;

        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];
            if (isEscaped) { isEscaped = false; continue; }
            if (c == '\\') { isEscaped = true; continue; }
            if (c == '"') { inString = !inString; }
            if (inString) continue;
            if (c == '{') { depth++; }
            else if (c == '}')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    private int FindBytes(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || needle.Length > haystack.Length) return -1;
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack.Skip(i).Take(needle.Length).SequenceEqual(needle))
            {
                return i;
            }
        }
        return -1;
    }
}