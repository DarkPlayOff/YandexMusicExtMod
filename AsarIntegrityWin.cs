using System.Buffers;
using System.IO.MemoryMappedFiles;
using System.Security.Cryptography;
using System.Text;

namespace YandexMusicPatcherGui;

public class HashPatcher
{
    private const string EXE_FILENAME = "Яндекс Музыка.exe";
    private const string RESOURCES_FOLDERNAME = "resources";
    private const string ORIGINAL_ASAR_FILENAME = "oldapp.asar";
    private const string MODIFIED_ASAR_FILENAME = "app.asar";
    private const int HEADER_OFFSET = 16;
    private const int CHUNK_SIZE = 4 * 1024 * 1024;

    public async Task<bool> PatchExecutable(string yandexMusicDirectory)
    {
        try
        {
            var exePath = Path.Combine(yandexMusicDirectory, EXE_FILENAME);
            var resourcesPath = Path.Combine(yandexMusicDirectory, RESOURCES_FOLDERNAME);
            var originalAsarPath = Path.Combine(resourcesPath, ORIGINAL_ASAR_FILENAME);
            var modifiedAsarPath = Path.Combine(resourcesPath, MODIFIED_ASAR_FILENAME);

            var originalHash = await CalculateAsarHeaderHash(originalAsarPath).ConfigureAwait(false);
            var modifiedHash = await CalculateAsarHeaderHash(modifiedAsarPath).ConfigureAwait(false);

            if (string.IsNullOrEmpty(originalHash) || string.IsNullOrEmpty(modifiedHash))
                return false;

            if (originalHash == modifiedHash)
                return true;

            return await PatchExeFileMemoryMapped(exePath, originalHash, modifiedHash).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> PatchExeFileMemoryMapped(string exePath, string originalHash, string modifiedHash)
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

    private (bool Found, bool IsOriginalHash, long Offset) ParallelSearchInMemoryMappedFile(
        MemoryMappedFile mmf, long fileLength, byte[] originalHashBytes, byte[] modifiedHashBytes)
    {
        var chunks = (int)Math.Ceiling((double)fileLength / CHUNK_SIZE);
        var overlap = Math.Max(originalHashBytes.Length, modifiedHashBytes.Length);
        
        var result = (Found: false, IsOriginalHash: false, Offset: 0L);
        var resultLock = new object();

        Parallel.For(0, chunks, (chunkIndex, state) =>
        {
            var startOffset = chunkIndex * (long)CHUNK_SIZE;
            var chunkSize = (int)Math.Min(CHUNK_SIZE + overlap, fileLength - startOffset);
            
            if (chunkSize <= 0) return;

            try
            {
                using var accessor = mmf.CreateViewAccessor(startOffset, chunkSize);
                var buffer = ArrayPool<byte>.Shared.Rent(chunkSize);
                
                try
                {
                    accessor.ReadArray(0, buffer, 0, chunkSize);
                    var span = buffer.AsSpan(0, chunkSize);

                    var offset = FindBytes(span, originalHashBytes);
                    if (offset != -1)
                    {
                        lock (resultLock)
                        {
                            result = (true, true, startOffset + offset);
                        }
                        state.Stop();
                        return;
                    }

                    offset = FindBytes(span, modifiedHashBytes);
                    if (offset != -1)
                    {
                        lock (resultLock)
                        {
                            result = (true, false, startOffset + offset);
                        }
                        state.Stop();
                        return;
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            catch
            {
            }
        });

        return result;
    }

    public async Task<string?> CalculateAsarHeaderHash(string archivePath)
    {
        try
        {
            using var fileStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, 
                FileShare.Read, 4096, FileOptions.SequentialScan);
            
            if (fileStream.Length <= HEADER_OFFSET)
                return null;

            fileStream.Position = HEADER_OFFSET;
            
            var estimatedSize = Math.Min((int)(fileStream.Length - HEADER_OFFSET), 1024 * 1024);
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

    private static int FindBytes(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        return haystack.IndexOf(needle);
    }
}