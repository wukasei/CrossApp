using Core.Abstractions;
using Core.Events;

namespace Cli;

public sealed class ConsoleSink : INotificationSink
{
    public void Notify(LibraryChangedEventArgs e) =>
        Console.WriteLine($"[ConsoleSink] {e.At:HH:mm:ss} {e.Kind} {e.CopyId}");
}