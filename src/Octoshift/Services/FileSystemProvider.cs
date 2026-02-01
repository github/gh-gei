using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OctoshiftCLI.Services;

public class FileSystemProvider
{
    public virtual bool FileExists(string path) => File.Exists(path);

    public virtual bool DirectoryExists(string path) => Directory.Exists(path);

    public virtual Task<byte[]> ReadAllBytesAsync(string path) => File.ReadAllBytesAsync(path);

    public virtual DirectoryInfo CreateDirectory(string path) => Directory.CreateDirectory(path);

    public virtual FileStream Open(string path, FileMode mode) => File.Open(path, mode);

    public virtual Stream OpenRead(string path) => File.OpenRead(path);

    public virtual async Task WriteAllTextAsync(string path, string contents) => await File.WriteAllTextAsync(path, contents);

    public virtual async ValueTask WriteAsync(FileStream fileStream, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (fileStream is null)
        {
            return;
        }

        await fileStream.WriteAsync(buffer, cancellationToken);
    }

    public virtual void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public virtual string GetTempFileName() => Path.GetTempFileName();

    public virtual async Task CopySourceToTargetStreamAsync(Stream source, Stream target)
    {
        if (source != null)
        {
            await source.CopyToAsync(target);
        }
    }
}
