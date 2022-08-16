using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace OctoshiftCLI;

public class LfsShaMapper
{
    internal Func<string, string, Task> WriteToFile = async (path, contents) => await File.WriteAllTextAsync(path, contents);
    internal Func<string, string> ReadFile = fileName => File.ReadAllText(fileName);
    internal Func<string, IEnumerable<string>> ReadMappingFile = fileName => File.ReadLines(fileName);
    
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
        var lfsMappingLines = ReadMappingFile(lfsMappingFile);
        var lfsMappings = lfsMappingLines
            .Select(line => line.Split(','))
            .ToDictionary(kvp => kvp[0], kvp => kvp[1]);
        foreach (string fileName in fileNames)
        {
            string text = ReadFile(fileName);
            foreach(KeyValuePair<string, string> entry in lfsMappings)
            {
                text = text.Replace(entry.Key, entry.Value);
            }
            await WriteToFile(fileName, text);
        }

        _log.LogInformation("Done modifying pull_requests_*.json files in archive");

        return _archiveHandler.Pack(_archiveHandler.extractDir);
    }
}