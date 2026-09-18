# CrossApp
Наскрізний проєкт з крос-платформного програмування.
Предметна область: Бібліотека. Сутності: Book, BookCopy, Reader, Loan.
Призначення: облік видач примірників книг читачам.

## Структура рішення
- **Core** (class library) — містить допоміжний код для отримання інформації про середовище виконання. Не має точки входу. Зарезервовані каталоги: `Core/Dto/`, `Core/Domain/`, `Core/Storage/`.
- **Cli** (console app) — точка входу. Використовує `ProjectReference` на Core, лише форматує та виводить дані. Жодної бізнес-логіки.

## Команди
```bash
dotnet build
dotnet run --project src/Cli
dotnet publish src/Cli -c Release -r win-x64 --self-contained true -f net10.0
dotnet publish src/Cli -c Release -r win-x64 --self-contained false -f net10.0
```

## Середовище
.NET SDK 10.0, Windows 11 x64 

## Порівняння режимів публікації
| RID       | Режим                | Розмір publish | Потрібен runtime |
|-----------|----------------------|----------------|------------------|
| win-x64   | self-contained       | ~76.7 МБ       | ні               |
| win-x64   | framework-dependent  | ~0.19 МБ       | так (.NET 10)    |
| linux-x64 | self-contained       | ~70.5 МБ       | ні               |

*Розміри каталогів self-contained для різних ОС практично ідентичні, оскільки базовий склад нативного середовища виконання .NET Runtime (CoreCLR, компілятор RyuJIT та набір системних бібліотек BCL) оптимізовано однаково для обох платформ x64.*