using System;

namespace OmsiStudio.App.Services;

public sealed class SynchronousProgress<T> : IProgress<T>
{
    private readonly Action<T> _handler;

    public SynchronousProgress(Action<T> handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public void Report(T value)
    {
        _handler(value);
    }
}
