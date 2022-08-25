using System.IO;

namespace OctoshiftCLI.Contracts;

public interface IFileSystemProvider
{
    bool FileExists(string path);
    DirectoryInfo CreateDirectory(string path);
}
