using System;
using System.Threading.Tasks;

namespace OmsiStudio.App.Services;

public sealed class InlineUiDispatcher : IUiDispatcher
{
    public bool CheckAccess() => true;

    public Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
        return Task.CompletedTask;
    }
}
