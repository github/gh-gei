using System.CommandLine;
using System.CommandLine.Binding;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.BbsToGithub.ModelBinders;

public class GithubApiBinder : BinderBase<GithubApi>
{
    private readonly ServiceProvider _sp;
    private readonly Option<string> _apiUrlOption;
    private readonly Option<string> _personalAccessTokenOption;

    public GithubApiBinder(ServiceProvider sp, Option<string> apiUrlOption, Option<string> personalAccessTokenOption)
    {
        _sp = sp;
        _apiUrlOption = apiUrlOption;
        _personalAccessTokenOption = personalAccessTokenOption;
    }

    protected override GithubApi GetBoundValue(BindingContext bindingContext)
    {
        var githubApiFactory = _sp.GetRequiredService<GithubApiFactory>();
        var environmentVariableProvider = _sp.GetRequiredService<EnvironmentVariableProvider>();
        var apiUrl = _apiUrlOption.HasValue() ? bindingContext.ParseResult.GetValueForOption(_apiUrlOption) : null;
        var personalAccessToken = bindingContext.ParseResult.GetValueForOption(_personalAccessTokenOption) ??
                                  environmentVariableProvider.GithubPersonalAccessToken();
        return githubApiFactory.Create(apiUrl, personalAccessToken);
    }
}
