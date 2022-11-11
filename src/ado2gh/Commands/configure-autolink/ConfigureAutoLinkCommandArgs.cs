namespace OctoshiftCLI.AdoToGithub.Commands;

public class ConfigureAutoLinkCommandArgs
{
    public string GithubOrg { get; set; }
    public string GithubRepo { get; set; }
    public string AdoOrg { get; set; }
    public string AdoTeamProject { get; set; }
    public string GithubPat { get; set; }
    public bool Verbose { get; set; }
}
