using System.CommandLine;
using System.CommandLine.Binding;
using System.Linq;

namespace OctoshiftCLI.Services;

public class GenericArgsBinder<TCommand, TArgs> : BinderBase<TArgs>
    where TCommand : notnull
    where TArgs : class, new()
{
    private readonly TCommand _command;

    public GenericArgsBinder(TCommand command) => _command = command;

    protected override TArgs GetBoundValue(BindingContext bindingContext)
    {
        var args = new TArgs();

        foreach (var prop in typeof(TCommand).GetProperties().Where(p => p.PropertyType.IsAssignableTo(typeof(Option))))
        {
            typeof(TArgs)
                .GetProperty(prop.Name)?
                .SetValue(args, bindingContext?.ParseResult.GetValueForOption((Option)prop.GetValue(_command)!));
        }

        return args;
    }
}
