using Core.Abstractions;
using Core.Events;

namespace Cli;

public sealed class FileSink(string path) : INotificationSink
{
    public void Notify(LibraryChangedEventArgs e) =>
        File.AppendAllText(path, $"[FileSink] {e.At:HH:mm:ss} {e.Kind} {e.CopyId}{Environment.NewLine}");
}