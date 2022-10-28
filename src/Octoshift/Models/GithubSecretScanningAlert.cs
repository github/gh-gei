namespace Octoshift.Models;
public class GithubSecretScanningAlert
{
    public int Number { get; set; }
    public string State { get; set; }
    public string Resolution { get; set; }
    public string SecretType { get; set; }
    public string Secret { get; set; }
}

public class GithubSecretScanningAlertLocation
{
    public string Path { get; set; }
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public int StartColumn { get; set; }
    public int EndColumn { get; set; }
    public string BlobSha { get; set; }
}
