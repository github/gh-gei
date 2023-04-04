using System;
using System.Threading.Tasks;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.GithubEnterpriseImporter.Services;

public class GhesVersionChecker
{
    private readonly OctoLogger _log;

    public GhesVersionChecker(OctoLogger log)
    {
        _log = log;
    }

    public virtual async Task<bool> AreBlobCredentialsRequired(string ghesApiUrl, GithubApi api)
    {
        var blobCredentialsRequired = false;

        if (ghesApiUrl.HasValue())
        {
            blobCredentialsRequired = true;

            _log.LogInformation("Using GitHub Enterprise Server - verifying server version");
            var ghesVersion = await api?.GetEnterpriseServerVersion();

            if (ghesVersion != null)
            {
                _log.LogInformation($"GitHub Enterprise Server version {ghesVersion} detected");

                if (Version.TryParse(ghesVersion, out var parsedVersion))
                {
                    blobCredentialsRequired = parsedVersion < new Version(3, 8, 0);
                }
                else
                {
                    _log.LogInformation($"Unable to parse the version number, defaulting to using CLI for blob storage uploads");
                }
            }
        }

        return blobCredentialsRequired;
    }
}
