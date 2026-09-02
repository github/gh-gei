using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OctoshiftCLI.Commands;
using OctoshiftCLI.Extensions;
using OctoshiftCLI.Services;

namespace OctoshiftCLI.GitlabToGithub.Commands.DiagnoseGitlabExport;

public class DiagnoseGitlabExportCommandHandler : ICommandHandler<DiagnoseGitlabExportCommandArgs>
{
    private readonly OctoLogger _log;
    private readonly GitlabApi _gitlabApi;
    private readonly FileSystemProvider _fileSystemProvider;

    public DiagnoseGitlabExportCommandHandler(OctoLogger log, GitlabApi gitlabApi, FileSystemProvider fileSystemProvider)
    {
        _log = log;
        _gitlabApi = gitlabApi;
        _fileSystemProvider = fileSystemProvider;
    }

    public async Task Handle(DiagnoseGitlabExportCommandArgs args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        var output = args.Output.HasValue()
            ? args.Output
            : $"gitlab-export-diagnostics-{SanitizeFileName(args.GitlabGroup)}-{SanitizeFileName(args.GitlabProject)}.md";

        if (_fileSystemProvider.FileExists(output) && !args.Overwrite)
        {
            throw new OctoshiftCliException($"File {output} already exists! Use --overwrite to overwrite this file.");
        }

        _log.LogInformation("Collecting GitLab export diagnostics...");

        var (version, enterprise) = await _gitlabApi.GetServerVersion();
        var projectDetails = await _gitlabApi.GetProjectDetails(args.GitlabGroup, args.GitlabProject);
        var exportDetails = await _gitlabApi.GetExportDetails(args.GitlabGroup, args.GitlabProject);

        await _fileSystemProvider.WriteAllTextAsync(output, BuildReport(args, version, enterprise, projectDetails, exportDetails));

        if (string.Equals(exportDetails.ExportStatus, "failed", StringComparison.OrdinalIgnoreCase))
        {
            _log.LogWarning("GitLab reported the project export as failed before GitHub received an archive. Ask a GitLab administrator to inspect the project export job on the GitLab instance using the commands in the report.");
        }

        _log.LogSuccess($"Wrote GitLab export diagnostics to {output}.");
    }

    private static string BuildReport(
        DiagnoseGitlabExportCommandArgs args,
        string version,
        bool enterprise,
        GitlabProjectDetails projectDetails,
        GitlabExportDetails exportDetails)
    {
        var projectPath = $"{args.GitlabGroup}/{args.GitlabProject}";
        var quotedProjectPath = ShellQuote(projectPath);
        var builder = new StringBuilder();

        builder.AppendLine("# GitLab export diagnostics");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine($"- GitLab server: {args.GitlabServerUrl.TrimEnd('/')}");
        builder.AppendLine($"- GitLab version: {ValueOrUnknown(version)} ({(enterprise ? "Enterprise" : "Community")} Edition)");
        builder.AppendLine($"- Project path: {projectPath}");
        builder.AppendLine($"- Project ID: {ValueOrUnknown(projectDetails.Id)}");
        builder.AppendLine($"- Export status: {ValueOrUnknown(exportDetails.ExportStatus)}");
        builder.AppendLine($"- Export ID: {ValueOrUnknown(exportDetails.Id)}");
        builder.AppendLine();
        builder.AppendLine("## Project details");
        builder.AppendLine();
        builder.AppendLine($"- Web URL: {ValueOrUnknown(projectDetails.WebUrl)}");
        builder.AppendLine($"- Visibility: {ValueOrUnknown(projectDetails.Visibility)}");
        builder.AppendLine($"- Archived: {ValueOrUnknown(projectDetails.Archived)}");
        builder.AppendLine($"- Repository size: {ValueOrUnknown(projectDetails.RepositorySize)} bytes");
        builder.AppendLine($"- Uploads size: {ValueOrUnknown(projectDetails.UploadsSize)} bytes");
        builder.AppendLine($"- Job artifacts size: {ValueOrUnknown(projectDetails.JobArtifactsSize)} bytes");
        builder.AppendLine();
        builder.AppendLine("## GitLab admin follow-up commands");
        builder.AppendLine();
        builder.AppendLine("Run these commands on the GitLab instance to retrieve the server-side export job error. GitLab does not expose these logs through the project export API.");
        builder.AppendLine();
        builder.AppendLine("```bash");
        builder.AppendLine($"sudo gitlab-rails runner \"p = Project.find_by_full_path({quotedProjectPath}); puts p.import_state.slice(:jid, :status, :last_error)\"");
        builder.AppendLine("sudo grep '<JID_FROM_ABOVE>' /var/log/gitlab/sidekiq/current");
        builder.AppendLine("sudo grep -iE 'export|import/export|backtrace|failed|error' /var/log/gitlab/sidekiq/current");
        builder.AppendLine("sudo tail -n 200 /var/log/gitlab/gitlab-rails/importer.log");
        builder.AppendLine("sudo gitlab-ctl tail sidekiq");
        builder.AppendLine("sudo gitlab-ctl tail gitlab-rails");
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine("## Raw GitLab export API response");
        builder.AppendLine();
        builder.AppendLine("```json");
        builder.AppendLine(exportDetails.RawJson);
        builder.AppendLine("```");

        return builder.ToString();
    }

    private static string ShellQuote(string value) => $"'{value.Replace("'", "'\\''")}'";

    private static string SanitizeFileName(string value) => Regex.Replace(value, "[^A-Za-z0-9_.-]+", "-").Trim('-');

    private static string ValueOrUnknown(object value) => value?.ToString() ?? "unknown";
}
