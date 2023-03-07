using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OctoshiftCLI;

public class FileSystemProvider
{
    public virtual bool FileExists(string path) => File.Exists(path);

    public virtual Task<byte[]> ReadAllBytesAsync(string path) => File.ReadAllBytesAsync(path);

    public virtual DirectoryInfo CreateDirectory(string path) => Directory.CreateDirectory(path);

    public virtual FileStream Open(string path, FileMode mode) => File.Open(path, mode);

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

    public virtual string GetTempPath() => System.IO.Path.GetTempPath();
}
