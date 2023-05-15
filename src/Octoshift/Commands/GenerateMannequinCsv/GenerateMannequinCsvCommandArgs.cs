using System.IO;

namespace OctoshiftCLI.Commands.GenerateMannequinCsv;

public class GenerateMannequinCsvCommandArgs
{
    public string GithubOrg { get; set; }
    public FileInfo Output { get; set; }
    public bool IncludeReclaimed { get; set; }
    public string GithubPat { get; set; }
    public bool Verbose { get; set; }
}
