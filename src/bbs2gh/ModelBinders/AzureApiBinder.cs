using System.CommandLine;
using System.CommandLine.Binding;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.BbsToGithub.ModelBinders;

public class AzureApiBinder : BinderBase<AzureApi>
{
    private readonly ServiceProvider _sp;
    private readonly Option<string> _azureStorageConnectionStringOption;
    private readonly Option<bool> _noSslOption;

    public AzureApiBinder(ServiceProvider sp, Option<string> azureStorageConnectionStringOption, Option<bool> noSslOption)
    {
        _sp = sp;
        _azureStorageConnectionStringOption = azureStorageConnectionStringOption;
        _noSslOption = noSslOption;
    }

    protected override AzureApi GetBoundValue(BindingContext bindingContext)
    {
        var azureApiFactory = _sp.GetRequiredService<IAzureApiFactory>();
        var environmentVariableProvider = _sp.GetRequiredService<EnvironmentVariableProvider>();
        var azureStorageConnectionString = bindingContext.ParseResult.GetValueForOption(_azureStorageConnectionStringOption) ??
                                           environmentVariableProvider.AzureStorageConnectionString();
        var noSsl = _noSslOption.HasValue() && bindingContext.ParseResult.GetValueForOption(_noSslOption);
        return noSsl ? azureApiFactory.CreateClientNoSsl(azureStorageConnectionString) : azureApiFactory.Create(azureStorageConnectionString);
    }
}
