using System.IO;
using System.Threading.Tasks;

namespace OctoshiftCLI;

public class FileSystemProvider
{
    public virtual bool FileExists(string path) => File.Exists(path);

    public virtual DirectoryInfo CreateDirectory(string path) => Directory.CreateDirectory(path);

    public virtual FileStream Open(string path, FileMode mode) => File.Open(path, mode);

    public virtual async Task WriteAllTextAsync(string path, string contents) => await File.WriteAllTextAsync(path, contents);
}
