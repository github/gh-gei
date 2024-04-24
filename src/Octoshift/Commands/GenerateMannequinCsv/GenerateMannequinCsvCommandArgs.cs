using System.IO;

namespace OctoshiftCLI.Commands.GenerateMannequinCsv;

public class GenerateMannequinCsvCommandArgs : CommandArgs
{
    public string GithubOrg { get; set; }
    public FileInfo Output { get; set; }
    public bool IncludeReclaimed { get; set; }
    [Secret]
    public string GithubPat { get; set; }
    public string TargetApiUrl { get; set; }
}
