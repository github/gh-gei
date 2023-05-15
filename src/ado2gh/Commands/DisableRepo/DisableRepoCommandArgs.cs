namespace OctoshiftCLI.AdoToGithub.Commands.DisableRepo
{
    public class DisableRepoCommandArgs
    {
        public string AdoOrg { get; set; }
        public string AdoTeamProject { get; set; }
        public string AdoRepo { get; set; }
        public string AdoPat { get; set; }
        public bool Verbose { get; set; }
    }
}
