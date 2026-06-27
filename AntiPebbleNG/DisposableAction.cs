using System;

namespace AntiPebbleNG;

public sealed class DisposableAction(Action dispose) : IDisposable
{
    private Action? _dispose = dispose ?? throw new ArgumentNullException(nameof(dispose));
    public void Dispose() => Dispose(true);
    private void Dispose(bool disposing)
    {
        if (!disposing || _dispose == null)
            return;

        try { _dispose(); }
        catch (Exception)
        {
            /* ignored */
        }

        _dispose = null;
    }
}