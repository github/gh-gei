using System;
using System.CommandLine;
using System.CommandLine.Binding;
using Microsoft.Extensions.DependencyInjection;
using OctoshiftCLI.BbsToGithub.Services;

namespace OctoshiftCLI.BbsToGithub.ModelBinders;

public class BbsSshArchiveDownloaderBinder : BinderBase<IBbsArchiveDownloader>
{
    private readonly ServiceProvider _sp;
    private readonly Option<string> _bbsServerUrlOption;
    private readonly Option<string> _sshUserOption;
    private readonly Option<string> _sshPrivateKeyOption;
    private readonly Option<int> _sshPortOption;

    public BbsSshArchiveDownloaderBinder(
        ServiceProvider sp, 
        Option<string> bbsServerUrlOption, 
        Option<string> sshUserOption,
        Option<string> sshPrivateKeyOption,
        Option<int> sshPortOption)
    {
        _sp = sp;
        _bbsServerUrlOption = bbsServerUrlOption;
        _sshUserOption = sshUserOption;
        _sshPrivateKeyOption = sshPrivateKeyOption;
        _sshPortOption = sshPortOption;
    }

    protected override IBbsArchiveDownloader GetBoundValue(BindingContext bindingContext)
    {
        var bbsArchiveDownloaderFactory = _sp.GetRequiredService<BbsArchiveDownloaderFactory>();
        var bbsServerUrl = bindingContext.ParseResult.GetValueForOption(_bbsServerUrlOption);
        var sshUser = bindingContext.ParseResult.GetValueForOption(_sshUserOption);
        var privateKeyFileFullPath = bindingContext.ParseResult.GetValueForOption(_sshPrivateKeyOption);
        var sshPort = bindingContext.ParseResult.GetValueForOption(_sshPortOption);
        return bbsArchiveDownloaderFactory.CreateSshDownloader(ExtractHost(bbsServerUrl), sshUser, privateKeyFileFullPath, sshPort);
    }
    
    private string ExtractHost(string bbsServerUrl) => new Uri(bbsServerUrl).Host;
}
