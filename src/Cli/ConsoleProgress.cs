namespace Cli;

// Додаткове 3: малює відсотки в одному рядку консолі
public sealed class ConsoleProgress : IProgress<int>
{
    private bool _lineOpen;

    public void Report(int value)
    {
        Console.Write($"\rІмпорт: {value,3}%");   // \r — повернутися на початок рядка
        _lineOpen = true;
    }

    // перейти на новий рядок, щоб наступне повідомлення не злиплося з відсотками
    public void EndLine()
    {
        if (_lineOpen) Console.WriteLine();
        _lineOpen = false;
    }
}