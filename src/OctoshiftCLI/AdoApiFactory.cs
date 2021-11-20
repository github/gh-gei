namespace OctoshiftCLI;

public class AdoApiFactory : IDisposable
{
    private AdoApi _api;
    private string _token;
    private readonly OctoLogger _log;
    private bool disposedValue;

    public AdoApiFactory(OctoLogger log) => _log = log;
    public AdoApiFactory(AdoApi api) => _api = api;
    public AdoApiFactory(string token) => _token = token;

    public AdoApi Create()
    {
        if (_api != null)
        {
            return _api;
        }

        var adoToken = GetAdoToken();
        var client = new AdoClient(_log, adoToken);
        _api = new AdoApi(client);

        return _api;
    }

    public string GetAdoToken()
    {
        if (!string.IsNullOrWhiteSpace(_token))
        {
            return _token;
        }

        var adoToken = Environment.GetEnvironmentVariable("ADO_PAT");

        if (string.IsNullOrWhiteSpace(adoToken))
        {
            _log.LogError("NO ADO_PAT FOUND IN ENV VARS, exiting...");
            return null;
        }

        _token = adoToken;
        return _token;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _api?.Dispose();
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}