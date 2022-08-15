using System;
using System.IO;
using System.Threading.Tasks;

namespace OctoshiftCLI;

public class LfsShaMapper
{
    internal Func<string, string, Task> WriteToFile = async (path, contents) => await File.WriteAllTextAsync(path, contents);
    internal Func<string, string> ReadFile = fileName => File.ReadAllText(fileName);
    
    private readonly OctoLogger _log;
    private readonly ArchiveHandler _archiveHandler;

    public LfsShaMapper(OctoLogger log, ArchiveHandler archiveHandler)
    {
        _log = log;
        _archiveHandler = archiveHandler;
    }

    public virtual async Task<Byte[]> MapShas(byte[] metadataArchiveContent, string lfsMappingFile)
    {
        _log.LogInformation("Modifying pull_requests_*.json files in archive");
        
        var fileNames = _archiveHandler.Unpack(metadataArchiveContent);
            
        // modify the pull_requests_*.json files in the extracted archive
        foreach (string fileName in fileNames)
        {
            string text = ReadFile(fileName);
            var lfsMappingLines = File.ReadLines(lfsMappingFile);
            foreach (var lfsMappingLine in lfsMappingLines)
            {
                var lfsMapping = lfsMappingLine.Split(','); 
                text = text.Replace(lfsMapping[0], lfsMapping[1]);
            }
            await WriteToFile(fileName, text);
        }

        _log.LogInformation("Done modifying pull_requests_*.json files in archive");

        return _archiveHandler.Pack(_archiveHandler.extractDir);
    }
}