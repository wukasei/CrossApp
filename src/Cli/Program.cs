// See https://aka.ms/new-console-template for more information
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;

Console.OutputEncoding = Encoding.UTF8;

bool isJson = args.Contains("--json");

var sysInfo = new
{
    Student = "Інна Богачук",
    Group = "32",
    OSDescription = RuntimeInformation.OSDescription,
    OSVersion = Environment.OSVersion.ToString(),
    ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
    DotNetVersion = Environment.Version.ToString(),
    Runtime = RuntimeInformation.FrameworkDescription,
    AppBaseDirectory = AppContext.BaseDirectory,
    CurrentDirectory = Environment.CurrentDirectory,
    Domain = "Бібліотека (видання, примірник, читач, видача)"
};

if(isJson){
    var options = new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };
    Console.WriteLine(JsonSerializer.Serialize(sysInfo, options));
}
else{
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
}

