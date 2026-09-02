using System.IO;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GitlabToGithub.Commands.DiagnoseGitlabExport;

public class DiagnoseGitlabExportCommandArgs : CommandArgs
{
    public string GitlabServerUrl { get; set; }
    public string GitlabGroup { get; set; }
    public string GitlabProject { get; set; }
    [Secret]
    public string GitlabPat { get; set; }
    public string Output { get; set; }
    public bool Overwrite { get; set; }
    public bool NoSslVerify { get; set; }

    public override void Validate(OctoLogger log)
    {
        if (GitlabServerUrl.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("--gitlab-server-url must be provided.");
        }

        if (GitlabGroup.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("--gitlab-group must be provided.");
        }

        if (GitlabProject.IsNullOrWhiteSpace())
        {
            throw new OctoshiftCliException("--gitlab-project must be provided.");
        }

        if (Output.HasValue() && Directory.Exists(Output))
        {
            throw new OctoshiftCliException("--output must be a file path, not a directory.");
        }
    }
}
