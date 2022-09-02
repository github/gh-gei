using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OctoshiftCLI;

public class LfsShaMapper
{
    private readonly ArchiveHandler _archiveHandler;

    private readonly OctoLogger _log;
    internal Func<string, string> ReadFile = File.ReadAllText;
    internal Func<string, IEnumerable<string>> ReadMappingFile = File.ReadLines;
    internal Func<string, string, Task> WriteToFile = async (path, contents) => await File.WriteAllTextAsync(path, contents);

    public LfsShaMapper(OctoLogger log, ArchiveHandler archiveHandler)
    {
        _log = log;
        _archiveHandler = archiveHandler;
    }

    public virtual async Task<byte[]> MapShas(byte[] metadataArchiveContent, string lfsMappingFile)
    {
        _log.LogInformation("Modifying metadata files in archive");

        var fileNames = _archiveHandler.Unpack(metadataArchiveContent);

        // modify the pull_requests_*.json files in the extracted archive
        var lfsMappingLines = ReadMappingFile(lfsMappingFile);

        var lfsMappings = lfsMappingLines
            .Select(line => line.Split(','))
            .ToDictionary(kvp => kvp[0], kvp => kvp[1]);

        foreach (var fileName in fileNames)
        {
            var text = ReadFile(fileName);

            foreach (var entry in lfsMappings)
            {
                text = text.Replace(entry.Key, entry.Value);
            }

            await WriteToFile(fileName, text);
        }

        _log.LogInformation("Done modifying metadata files in archive");

        return _archiveHandler.Pack(_archiveHandler.ExtractDir);
    }
}
