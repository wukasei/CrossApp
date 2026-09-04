// See https://aka.ms/new-console-template for more information
using System.Runtime.InteropServices;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("CrossApp – практикум з крос-платформного програмування");
Console.WriteLine("Студент: Інна Богачук, група 32");
Console.WriteLine(new string('-', 52));
Console.WriteLine($"ОС (OSDescription)  : {RuntimeInformation.OSDescription}");
Console.WriteLine($"ОС (Environment)    : {Environment.OSVersion}");
Console.WriteLine($"Архітектура процесу : {RuntimeInformation.ProcessArchitecture}");
Console.WriteLine($"Версія .NET (CLR)   : {Environment.Version}");
Console.WriteLine($"Runtime             : {RuntimeInformation.FrameworkDescription}");
Console.WriteLine($"Каталог застосунку  : {AppContext.BaseDirectory}");
Console.WriteLine($"Поточний каталог    : {Environment.CurrentDirectory}");
Console.WriteLine(new string('-', 52));
Console.WriteLine("Предметна область: Бібліотека (видання, примірник, читач, видача)");