using System.CommandLine;
using System.CommandLine.Binding;
using Microsoft.Extensions.DependencyInjection;

namespace OctoshiftCLI.BbsToGithub.ModelBinders;

public class BbsApiBinder : BinderBase<BbsApi>
{
    private readonly ServiceProvider _sp;
    private readonly Option<string> _bbsServerUrlOption;
    private readonly Option<string> _bbsUsernameOption;
    private readonly Option<string> _bbsPasswordOption;

    public BbsApiBinder(ServiceProvider sp, Option<string> bbsServerUrlOption, Option<string> bbsUsernameOption, Option<string> bbsPasswordOption)
    {
        _sp = sp;
        _bbsServerUrlOption = bbsServerUrlOption;
        _bbsUsernameOption = bbsUsernameOption;
        _bbsPasswordOption = bbsPasswordOption;
    }

    protected override BbsApi GetBoundValue(BindingContext bindingContext)
    {
        var bbsApiFactory = _sp.GetRequiredService<BbsApiFactory>();
        var bbsServerUrl = bindingContext.ParseResult.GetValueForOption(_bbsServerUrlOption);
        var bbsUsername = bindingContext.ParseResult.GetValueForOption(_bbsUsernameOption);
        var bbsPassword = bindingContext.ParseResult.GetValueForOption(_bbsPasswordOption);
        return bbsApiFactory.Create(bbsServerUrl, bbsUsername, bbsPassword);
    }
}
