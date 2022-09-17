using System.CommandLine;
using System.CommandLine.Binding;
using System.Linq;

namespace OctoshiftCLI.BbsToGithub.ModelBinders;

public class GenericArgsBinder<TOptions, TArgs> : BinderBase<TArgs> 
    where TOptions : notnull
    where TArgs : class, new()
{
    private readonly TOptions _options;

    public GenericArgsBinder(TOptions options) => _options = options;

    protected override TArgs GetBoundValue(BindingContext bindingContext)
    {
        var args = new TArgs();
        
        foreach (var prop in _options.GetType().GetProperties().Where(p => p.GetValue(_options) is Option))
        {
            args.GetType()
                .GetProperty(prop.Name)?
                .SetValue(args, bindingContext.ParseResult.GetValueForOption((Option)prop.GetValue(_options)!));
        }

        return args;
    }
}
