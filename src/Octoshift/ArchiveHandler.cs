using System.IO;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.GZip;

namespace OctoshiftCLI;

public class ArchiveHandler
{
    private readonly OctoLogger _log;

    public string extractDir { get; }

    public ArchiveHandler(OctoLogger log)
    {
        _log = log;
        extractDir = "./archiveExtracted";
    }

    public virtual string[] Unpack(byte[] metadataArchiveContent)
    {
        _log.LogInformation("Unpacking archive");

        using var gzipData = new MemoryStream(metadataArchiveContent);
        using var inStream = new GZipInputStream(gzipData);
        using var archive = TarArchive.CreateInputTarArchive(inStream, TarBuffer.DefaultBlockFactor);
        archive.ExtractContents(extractDir);

        _log.LogInformation("Done unpacking archive");

        return Directory.GetFiles(extractDir);
    }

    public virtual byte[] Pack(string archivePath)
    {
        _log.LogInformation("Packing archive");

        // tar gz the modified archive
        // TODO: figure out how to put this into a byte array rather than a file!!
        const string tarFileName = "./archive.tar.gz";
        using var outStream = File.Create(tarFileName);
        using var gzOutStream = new GZipOutputStream(outStream);
        using var newArchive = TarArchive.CreateOutputTarArchive(gzOutStream, TarBuffer.DefaultBlockFactor);
        newArchive.RootPath = archivePath;
        
        string[] fileNames = Directory.GetFiles(archivePath);
        foreach (string name in fileNames) 
        {
            var entry = TarEntry.CreateEntryFromFile(name);
            newArchive.WriteEntry(entry, true);
        }
        newArchive.Close();
        var newArchiveContent = File.ReadAllBytes(tarFileName);
        File.Delete(tarFileName);
        Directory.Delete(archivePath, true);

        _log.LogInformation("Done packing archive");

        return newArchiveContent;
    }
}