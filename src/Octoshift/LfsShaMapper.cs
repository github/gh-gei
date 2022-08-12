using System;
using System.IO;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.GZip;

namespace OctoshiftCLI;

public class LfsShaMapper
{
    internal Func<string, string, Task> WriteToFile = async (path, contents) => await File.WriteAllTextAsync(path, contents);
    
    private readonly OctoLogger _log;

    public LfsShaMapper(OctoLogger log)
    {
        _log = log;
    }

    public async Task<Byte[]> MapShas(byte[] metadataArchiveContent, string lfsMappingFile)
    {
        _log.LogInformation("Modifying pull_requests_*.json files in archive");
        
        var extractDir = Unpack(metadataArchiveContent);
            
        // modify the pull_requests_*.json files in the extracted archive
        var fileNames = Directory.GetFiles(extractDir);
        foreach (string fileName in fileNames)
        {
            string text = File.ReadAllText(fileName);
            var lfsMappingLines = File.ReadLines(lfsMappingFile);
            foreach (var lfsMappingLine in lfsMappingLines)
            {
                var lfsMapping = lfsMappingLine.Split(','); 
                text = text.Replace(lfsMapping[0], lfsMapping[1]);
            }
            await WriteToFile(fileName, text);
        }

        _log.LogInformation("Done modifying pull_requests_*.json files in archive");

        return Pack(extractDir);
    }

    private string Unpack(byte[] metadataArchiveContent)
    {
        _log.LogInformation("Unpacking metadata archive");

        const string extractDir = "./archiveExtracted";
        using var gzipData = new MemoryStream(metadataArchiveContent);
        using var inStream = new GZipInputStream(gzipData);
        using var archive = TarArchive.CreateInputTarArchive(inStream, TarBuffer.DefaultBlockFactor);
        archive.ExtractContents(extractDir);
        
        _log.LogInformation("Done unpacking metadata archive");

        return extractDir;
    }

    private byte[] Pack(string archivePath)
    {
        _log.LogInformation("Packing metadata archive");

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

        var newArchiveContent = File.ReadAllBytes(tarFileName);
        File.Delete(tarFileName);
        Directory.Delete(archivePath, true);
        
        _log.LogInformation("Done packing metadata archive");

        return newArchiveContent;
    }
}