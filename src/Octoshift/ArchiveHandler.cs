using System.IO;
using System.Text;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;

namespace OctoshiftCLI;

public class ArchiveHandler
{
    private readonly OctoLogger _log;

    public string ExtractDir { get; }

    public ArchiveHandler(OctoLogger log)
    {
        _log = log;
        ExtractDir = "./archiveExtracted";
    }

    public virtual byte[] Pack(string archivePath)
    {
        _log.LogInformation("Packing archive");

        using var memoryStream = new MemoryStream();
        using var gzOutputStream = new GZipOutputStream(memoryStream);
        using var newTarArchive = TarArchive.CreateOutputTarArchive(gzOutputStream, TarBuffer.DefaultBlockFactor);

        newTarArchive.RootPath = archivePath;

        var fileNames = Directory.GetFiles(archivePath);

        foreach (var name in fileNames)
        {
            var entry = TarEntry.CreateEntryFromFile(name);
            newTarArchive.WriteEntry(entry, true);
        }

        newTarArchive.Close();

        Directory.Delete(archivePath, true);

        _log.LogInformation("Done packing archive");

        return memoryStream.ToArray();
    }

    public virtual string[] Unpack(byte[] metadataArchiveContent)
    {
        _log.LogInformation("Unpacking archive");

        using var memoryStream = new MemoryStream(metadataArchiveContent);
        using var gzInputStream = new GZipInputStream(memoryStream);
        using var tarArchive = TarArchive.CreateInputTarArchive(gzInputStream, TarBuffer.DefaultBlockFactor, Encoding.UTF8);

        tarArchive.ExtractContents(ExtractDir);

        _log.LogInformation("Done unpacking archive");

        return Directory.GetFiles(ExtractDir);
    }
}
