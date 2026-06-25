using System;
using System.Threading.Tasks;

namespace OmsiStudio.App.Services;

public sealed class SynchronousProgress<T> : IProgress<T>
{
    private readonly Func<T, Task> _handler;

    public SynchronousProgress(Func<T, Task> handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public void Report(T value)
    {
        _handler(value).GetAwaiter().GetResult();
    }
}
