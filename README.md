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

## Лабораторна 6 — Доменні події, делегати, лямбди

- **Вид події:** `LibraryChangeKind` (`src/Core/Events`) — `BookAdded`, `CopyIssued`, `CopyReturned`.
- **Дані події:** `LibraryChangedEventArgs : EventArgs` (`src/Core/Events`) — `Kind`, `Isbn`, `CopyId`, `ReaderId`, `DueDate`, `At`. Властивості лише для читання; `ReaderId` і `DueDate` заповнюються тільки для видачі.
- **Подія:** `public event EventHandler<LibraryChangedEventArgs>? Changed` у `LendingService`. Піднімається через приватний метод `OnChanged` (`Changed?.Invoke(...)`) у кінці `AddBook`, `IssueCopy`, `ReturnCopy` — лише після успішної зміни стану й збереження в сховищі. Якщо інваріант `BookCopy` кидає виняток, подія не виникає.
- **Зміна сигнатури:** `IssueCopy(id, readerId)` — тепер приймає id читача; дата повернення обчислюється як сьогодні + `LoanPeriodDays` (14 днів).
- Core не пише в консоль і у файли: пошук у `src/Core` за `Console` і `logs` дає 0 результатів.

### Підписники (Cli)
- `OnLibraryChanged` — локальна функція; виводить рядок події в консоль і дописує його у `logs/app.log` поруч зі збіркою. Шлях будується через `Path.Combine`, каталог створюється `Directory.CreateDirectory`.
- Лямбда-лічильник `(_, _) => changes++` — замикання над локальною змінною `changes`.
- Обробники викликаються по черзі в порядку підписки: спочатку рядок логу, потім лічильник.
- Надалі подію оброблятимуть логер проєкту Api (тиждень 10) і компонент Blazor (тиждень 12); `LendingService` для цього змінювати не потрібно.

### Демонстрація
`dotnet run --project src/Cli`:
1. Додавання книжки й видача — два рядки подій у консолі та в `app.log`.
2. Сценарій відмови (неіснуючий id, повторна видача) — текст помилки, рядків подій немає.
3. Повернення книжки — рядок події, `Подій отримано: 3`.
4. `service.Changed -= OnLibraryChanged` і повторна видача — рядка логу немає, лічильник збільшився до 4, примірник видано. В `app.log` ця операція не потрапляє.

### Делегат Func
`LendingService.Search(Func<BookCopy, bool>)` (з лабораторної 5) використано як приклад, де потрібен результат: умову передає викликач, напр. `Search(c => c.IsIssued)`. Подія лише сповіщає і нічого не повертає, тому для пошуку застосовано `Func`.

### Додаткові завдання
- **INotificationSink** (`src/Core/Abstractions`) — інтерфейс з методом `Notify(LibraryChangedEventArgs)`. Реалізації лежать у Cli, бо пишуть у консоль і файл:
  - `ConsoleSink` (`src/Cli/ConsoleSink.cs`) — виводить рядок `[ConsoleSink] ...` у консоль;
  - `FileSink` (`src/Cli/FileSink.cs`) — дописує рядок `[FileSink] ...` у `logs/app.log`.

  `LendingService` приймає `IEnumerable<INotificationSink>?` другим необов'язковим параметром конструктора (ручний DI, як `IBookStore` у лабораторній 5). `OnChanged` спершу піднімає подію `Changed`, потім викликає `Notify` у кожного sink. У `Program.cs` створюється окремий сервіс `new LendingService(store, [new ConsoleSink(), new FileSink(logPath)])`: на його подію ніхто не підписаний, але повідомлення про повернення книжки надходить через sinks.

  **Подія vs INotificationSink:** подія не показує, хто слухає сервіс; підписатися й відписатися можна будь-коли (`+=` / `-=`), але забута відписка може тримати об'єкт у пам'яті. Sinks передаються явно через конструктор: залежність видно одразу, у тестах легко підставити фальшивий sink, проте змінити набір отримувачів після створення сервісу не можна.

- **Action<string> в імпорті:** `BookCopy.FromImport(ImportResult<BookDto>, Action<string>? onSkipped = null)` викликає `onSkipped` для кожного пропущеного рядка: помилок розбору й порушень інваріантів. Що робити з повідомленням, вирішує викликач; у Cli це лямбда `msg => Console.WriteLine($"  пропущено: {msg}")`. Параметр необов'язковий, тому попередні виклики `FromImport` працюють без змін.

## Лабораторна 7 — Звіти через LINQ

- **DTO** (`src/Core/Reports`): `CopyLine`, `GroupSummary`, `LoanLine` — незмінні `record`. Звіти повертають DTO, а не `BookCopy`, щоб назовні не потрапляли методи зміни стану і контракт не залежав від доменної моделі.
- **ReportService** — статичний клас. Приймає `IEnumerable<T>`, повертає `IReadOnlyList<DTO>` (через `ToList()`). Консоль не використовує.
- **SampleData** — 20 примірників (4 видано, 1 списано, кілька однакових книжок), 6 видач, генератор `Generate(n)`.
- `Program.cs` лише викликає звіти і друкує результат. Команда шукається через `args.FirstOrDefault(a => a.StartsWith("report-"))`, тому `--file` / `--cache` не сприймаються як назва звіту. Без команди запускається демо лабораторних 5–6.

### Запити
| Команда | Метод | Що показує | Оператори |
|---------|-------|------------|-----------|
| `report-issued` | `Issued` | примірники на руках | `Where`, `OrderBy`, `ThenBy`, `Select` |
| `report-top` | `TopNewest` | 5 найновіших видань | `TopBy`, `Select` |
| `report-groups` | `ByTitle` | примірники кожної книжки, з них видано | `GroupBy`, `Select`, `Count(умова)` |
| `report-sum` | `Totals` | кількість, видано, середня к-сть примірників на книжку, найновіший рік | `Count`, `Average`, `Max` |
| `report-demo` | `DeferredDemo` | відкладене виконання | `Where`, `Count()`, `ToList()` |

```bash
dotnet run --project src/Cli -- report-sum
 примірників 20, видано 4, в середньому 1,33 прим. на книжку, найновіший рік 2022
```

- **Стабільний топ-N:** другий ключ `ThenBy(Id, StringComparer.Ordinal)`. C-007 і C-020 (обидва 2019) завжди йдуть в одному порядку.
- **Порожнє джерело:** `Totals` при `Count == 0` повертає нулі, тому `Average` і `Max` не кидають виняток.

### Відкладене виконання
`source.Where(c => c.IsIssued)` лише описує запит, а виконується він під час `Count()` або `ToList()`. Перший `Count()` дав 4. Після додавання виданого примірника до джерела повторний `Count()` виконав запит заново і дав 5. Результат `ToList()` — окремий список, тому після `source.Clear()` у ньому лишилось 5.

### Додаткові завдання
- **Join** (`report-loans`): `LoanHistory` з'єднує примірники і видачі за `BookCopy.Id == Loan.BookCopyId` у DTO `LoanLine` з назвою книжки, читачем і датами.
- **Метод-розширення:** `TopBy<T>(this IEnumerable<T>, key, thenBy, n)` у `EnumerableExtensions.cs`. Використано в `TopNewest` (`report-top`) і `TopReaders` — топ читачів за видачами (`report-readers`).
- **Синтаксис запитів** (`report-groups-q`): `ByTitleQuery` — `ByTitle` через `from ... group ... into`. Групування так читається природніше, але в синтаксисі запитів не можна передати `StringComparer`, тому порядок груп з одним примірником відрізняється («Інститутка»). Для `Where`, `ThenBy`, `Take` зручніший методний синтаксис.
- **Stopwatch** (`report-bench`, Release, 100 000 записів, середнє з 20 запусків):

  | Варіант | Час |
  |---------|-----|
  | без `ToList` у середині | 3,62 мс |
  | з `ToList` у середині | 13,86 мс |

  Проміжні `ToList` створюють зайві списки й змушують повністю сортувати ~25 000 виданих примірників, хоча потрібні лише 5.