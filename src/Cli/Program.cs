using Core;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;

// Викликаємо логіку з бібліотеки Core
EnvironmentReport report = EnvironmentInfo.Collect();

// Cli займається лише друком результату
Console.WriteLine("CrossApp – інформація про середовище");
Console.WriteLine(new string('-', 52));
Console.WriteLine($"Версія збірки: {EnvironmentInfo.BuildNote}");
Console.WriteLine($"ОС : {report.OsDescription}");
Console.WriteLine($"Runtime : {report.FrameworkDescription}");
Console.WriteLine($"Архітектура : {report.ProcessArchitecture}");
Console.WriteLine($"RID (визначено): {report.DetectedRid}");
Console.WriteLine($"RID (від .NET) : {report.ReportedRid}");
Console.WriteLine($"Каталог : {report.BaseDirectory}");