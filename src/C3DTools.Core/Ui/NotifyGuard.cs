using System;
using System.ComponentModel;

namespace C3DTools.Core.Ui;

/// <summary>
/// Raises PropertyChanged for every dialog view model, with a fuse: a binding that writes back a value, which raises
/// again, which writes back again… is cut after MaxDepth nested raises instead of overflowing the stack (a stack
/// overflow cannot be caught and takes the host application down).
/// </summary>
public static class NotifyGuard
{
    public const int MaxDepth = 32;

    [ThreadStatic] private static int _depth;
    [ThreadStatic] private static bool _reported;

    /// <summary>Called once per loop with "Type.Property"; the host writes it to the log.</summary>
    public static Action<string> LoopDetected { get; set; }

    public static void Raise(object sender, PropertyChangedEventHandler handler, string name)
    {
        if (handler == null) return;
        if (_depth >= MaxDepth)
        {
            if (_reported) return;
            _reported = true;
            try
            {
                LoopDetected?.Invoke((sender?.GetType().Name ?? "?") + "." + name);
            }
            catch (Exception)
            {
                // Reporting must never make things worse.
            }

            return;
        }

        _depth++;
        try
        {
            handler(sender, new PropertyChangedEventArgs(name));
        }
        finally
        {
            _depth--;
            if (_depth == 0) _reported = false;
        }
    }
}
