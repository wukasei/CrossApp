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

## Лабораторна 4 — Доменна модель

### Сутності
- **BookCopy** (`src/Core/Domain/BookCopy.cs`) — примірник книжки. Створюється через `BookCopy.Create(...)`; стан (`CopyStatus`: на полиці / видано / списано) змінюється лише методами `Issue()`, `Return()`, `WriteOff()`.
- **Loan** (`src/Core/Domain/Loan.cs`) — видача примірника читачу. Відкривається через `Loan.Open(...)`, закривається методом `Close(...)`.
- **Book** (`src/Core/Domain/Book.cs`) — книга (твір з одним ISBN), містить колекцію своїх примірників. Список зберігається в `private readonly List<BookCopy>`, назовні віддається як `IReadOnlyList<BookCopy>` через `AsReadOnly()`; додати примірник можна лише методом `AddCopy`.

Зв'язок: книга (`Book`) містить кілька примірників (`BookCopy`) з тим самим ISBN. Одна видача (`Loan`) стосується одного примірника (через `BookCopyId`) і одного читача (через `ReaderId`). `Loan.Open` позначає примірник виданим, `Loan.Close` — повернутим.

Records з лабораторної 3 (`BookDto`, `LoanDto`) лишились як DTO. Перетворення між сутністю і DTO — методи `ToDto()` / `FromDto(dto)`; `FromDto` проходить ті самі перевірки, що й створення.

### Інваріанти

**BookCopy**
- Id примірника не порожній — `ArgumentException` — `Create`
- ISBN не порожній — `ArgumentException` — `Create`
- Назва книжки не порожня — `ArgumentException` — `Create`
- Рік видання від 1450 до поточного — `ArgumentOutOfRangeException` — `Create`
- Не можна видати вже виданий примірник — `InvalidOperationException` — `Issue`
- Не можна повернути невиданий примірник — `InvalidOperationException` — `Return`
- Не можна списати виданий примірник — `InvalidOperationException` — `WriteOff`
- Зі списаним примірником жодні операції неможливі — `InvalidOperationException` — `Issue`, `Return`, `WriteOff`

**Loan**
- Id видачі і Id читача не порожні — `ArgumentException` — `Open`, `FromDto`
- Примірник для видачі / повернення вказано — `ArgumentNullException` — `Open`, `Close`
- Не можна відкрити видачу на вже виданий примірник — `InvalidOperationException` — `Open` (через `BookCopy.Issue`)
- Не можна закрити вже закриту видачу — `InvalidOperationException` — `Close`
- Повертають саме той примірник, що був виданий — `InvalidOperationException` — `Close`
- Дата повернення не раніше дати видачі — `ArgumentOutOfRangeException` — `Close`, `FromDto`
- Дати у файлі мають коректний формат — `ArgumentException` — `FromDto`

**Book**
- ISBN і назва книги не порожні — `ArgumentException` — `Create`
- Примірник належить саме цій книзі (ISBN збігається) — `ArgumentException` — `AddCopy`
- Той самий примірник не можна додати двічі — `InvalidOperationException` — `AddCopy`

### Демонстрація
`dotnet run --project src/Cli` виводить два сценарії: успішну видачу й повернення книжки та спроби порушити інваріанти. Кожна спроба перехоплюється в `try/catch`, на екран виводиться тип винятку і повідомлення (без stack trace). Після всіх відмов стан об'єктів не змінюється. Окремий блок показує книгу з колекцією примірників і відмови `AddCopy`.

Перевірка залежностей: пошук у `src/Core/Domain` за `Console.` і `File.` дає 0 результатів — доменна модель не залежить ні від консолі, ні від файлової системи, ні від проєкту Cli.

### Додаткові завдання
- **Імпорт → сутності:** `BookCopy.FromImport(ImportResult<BookDto>)` повертає `ImportResult<BookCopy>`: прийняті примірники плюс помилки імпорту й порушення інваріантів.
- **Правило на дві сутності:** `LoanService.OpenLoan` (`src/Core/Services`) не дозволяє читачу мати більше 5 відкритих видач (`InvalidOperationException`). Правило винесено в сервіс, бо окрема сутність не знає про інші видачі читача: для перевірки потрібен перелік усіх видач зі сховища.
- **Стан через enum:** `CopyStatus { Available, Issued, WrittenOff }`; допустимі переходи перевіряються в `BookCopy.MoveTo` через switch expression. Списаний примірник не можна видати, виданий не можна списати.

## Лабораторна 5 — Сервісний шар, інтерфейси, ручний DI

- **Інтерфейс:** `IBookStore` (`src/Core/Abstractions`) — `List`, `GetById`, `Add`, `Update`, `Remove`.
- **Реалізації** (`src/Core/Storage`):
  - `InMemoryBookStore` — дані в пам'яті, стартові записи з `SampleData`, після виходу зникають;
  - `FileBookStore` — дані в `data/catalog.json` поруч зі збіркою (JSON-масив `BookDto`), зберігаються між запусками.
- **Сервіс:** `LendingService` (`src/Core/Services`) — `AddBook`, `IssueCopy`, `ReturnCopy`, `All`, `Find`. Отримує `IBookStore` через конструктор.
- **Схема:** `Cli → LendingService → IBookStore → InMemoryBookStore | FileBookStore`
- Конкретні класи створюються лише в `Cli`. DI-контейнер не підключено.

### Запуск
```bash
dotnet run --project src/Cli                    # пам'ять
dotnet run --project src/Cli -- --file          # файл
dotnet run --project src/Cli -- --cache         # пам'ять + кеш
dotnet run --project src/Cli -- --file --cache  # файл + кеш
```

### Додаткові завдання
- `CachingBookStore` — декоратор над будь-яким `IBookStore`, кешує `List()`.
- `LendingService.Search(Func<BookCopy, bool>)` — пошук за довільним правилом.
- `StoreFactory.Create(args)` (`src/Cli`) — вибір сховища за аргументами.