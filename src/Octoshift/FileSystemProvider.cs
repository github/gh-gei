using System.IO;
using OctoshiftCLI.Contracts;

namespace OctoshiftCLI;

public class FileSystemProvider : IFileSystemProvider
{
    public bool FileExists(string path) => File.Exists(path);

    public DirectoryInfo CreateDirectory(string path) => Directory.CreateDirectory(path);
}
