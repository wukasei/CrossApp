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

## Лабораторна 8 — async/await, скасування, доменні винятки, unit-тести

### Що стало асинхронним
- **Імпорт каталогу з файлу** — `IBookImporter.LoadAsync` (`src/Core/Abstractions`), реалізація `JsonBookImporter` (`src/Core/Storage`). Читання через `FileStream(..., useAsync: true)` і `JsonSerializer.DeserializeAsync`, без `Task.Run`. Це тривала операція введення-виведення, на великому файлі вона займає помітний час, і її має бути можливо скасувати.
- **`LendingService.ImportAsync(importer, path, ct)`** — імпортер приходить параметром (ручний DI, як `IBookStore` у лабораторній 5). Для кожного доданого примірника піднімається подія `BookAdded`. Примірники з уже наявним `Id` пропускаються, тому повторний імпорт нічого не дублює.
- **Синхронними лишились** `IBookStore` і його реалізації, `AddBook`, `IssueCopy`, `ReturnCopy`, звіти. Операції в пам'яті миттєві, а асинхронне сховище з'явиться разом з EF Core (тиждень 11).

### Скасування
Ланцюжок токена: `Program.cs` (`CancellationTokenSource`) → `ImportAsync(..., ct)` → `LoadAsync(path, ct)` → `DeserializeAsync(..., ct)`. У циклах викликається `ct.ThrowIfCancellationRequested()`. На початку `ImportAsync` і `LoadAsync` теж є перевірка: якщо токен уже скасовано, файл навіть не відкривається.

Джерела скасування в Cli: Ctrl+C (`Console.CancelKeyPress`, `e.Cancel = true`) і таймаут `--timeout <мс>`. `OperationCanceledException` перехоплюється окремим `catch`, раніше за інші, і виводиться «Імпорт скасовано.», а не стектрейс.

### Доменні винятки
`DomainException` (`src/Core/DomainException.cs`) — порушення бізнес-правила. Замінив `InvalidOperationException` у `BookCopy.MoveTo` (повторна видача, повернення невиданого, операції зі списаним), `Loan.Close`, `Book.AddCopy`, `LoanService.OpenLoan`, `LendingService` («немає примірника»). Типи винятків у таблиці інваріантів лабораторної 4 для цих правил тепер `DomainException`.

- `ArgumentException` — некоректний аргумент методу: порожній `readerId`, порожня назва, рік 1200.
- `DomainException` — аргументи коректні, але так не можна за правилами бібліотеки: видати вже виданий примірник.
- Технічні помилки (`FileNotFoundException`, `JsonException`) не маскуються під доменні: вони проходять до Cli, де обробляються окремо.

Під час імпорту `ArgumentException` із `BookCopy.FromDto` загортається в `DomainException("Запис N: ...")` з початковим винятком в `InnerException`, щоб не загубити причину.

Винятки сховищ («примірник уже існує» в `InMemoryBookStore`, `FileBookStore`) лишились `InvalidOperationException`: це помилка шару зберігання, а не бізнес-правило.

### Запуск
```bash
dotnet run --project src/Cli -- import data/import.json          # Імпортовано записів: 6
dotnet run --project src/Cli -- import data/import-bad.json      # Дані відхилено: Запис 3: Рік видання ...
dotnet run --project src/Cli -- import data/nema.json            # Файл не знайдено: data/nema.json
dotnet run --project src/Cli -- gen-import data/big.json 300000  # великий файл для демонстрації скасування
dotnet run --project src/Cli -- import data/big.json --timeout 100
```
Без `--timeout` можна натиснути Ctrl+C під час імпорту великого файлу. `data/big.json` додано в `.gitignore`.

### Тести
Проєкт `tests/Core.Tests` (xUnit), посилається лише на Core. Сховище в тестах — `InMemoryBookStore`, дані — власний `TestData.Items()` (5 примірників, два з однаковим роком), а не `SampleData`. Тести, яким потрібен файл, пишуть його в `Path.GetTempPath()` і видаляють у `finally`. `data/` у репозиторії тести не читають.

```bash
dotnet test
# Test summary: total: 16; failed: 0; succeeded: 16
```

| Тест | Що перевіряє | Очікуваний результат |
|------|--------------|----------------------|
| `IssueCopy_WithEmptyReader_ThrowsArgumentException_AndKeepsState` | порожній читач | `ArgumentException`, примірник на полиці |
| `IssueCopy_WhenAlreadyIssued_ThrowsDomainException_AndRaisesNoEvent` | повторна видача | `DomainException`, подій 0, стан не змінився |
| `ReturnCopy_WhenNotIssued_ThrowsDomainException_AndKeepsState` | повернення невиданого | `DomainException`, стан не змінився |
| `AddBook_RaisesChangedExactlyOnce` | подія з лабораторної 6 | рівно одна подія `BookAdded` |
| `TopNewest_ReturnsAtMostN` (`[Theory]`, 3 набори) | кількість у топ-N | 0→0, 3→3, 10→5 |
| `TopNewest_IsDeterministic` | стабільний порядок | T-05, T-02, T-03 |
| `ImportAsync_WhenAlreadyCancelled_Throws` | заздалегідь скасований токен | `OperationCanceledException` |
| `ImportAsync_Twice_AddsOnlyOnce` | ідемпотентність | 2, потім 0; подій 2 |
| `ImportAsync_WithInvalidRecord_ThrowsDomainException` | зіпсований запис у файлі | `DomainException` «Запис 2», `InnerException` збережено, сховище порожнє |
| `ImportStreamAsync_WhenAlreadyCancelled_Throws` | токен через `WithCancellation` | `OperationCanceledException` |
| `ImportStreamAsync_WithInvalidRecord_AddsRecordsBeforeError` | потоковий імпорт із помилкою | `DomainException` «Запис 3», у сховищі 2 записи |
| `ImportWithTimeout_WhenTimeoutExpires_ThrowsTimeoutException` | спрацював таймаут | `TimeoutException`, токен користувача не скасовано |
| `ImportWithTimeout_WhenUserCancels_ThrowsOperationCanceled` | скасував користувач | `OperationCanceledException`, не таймаут |
| `ImportAsync_ReportsProgress` | хід імпорту | 25, 50, 75, 100 |

Тест на подію не потребує ні консолі, ні диска: підписник — лямбда, що складає події в список.

### Самоперевірка
- Пошук по solution за `.Result`, `.Wait()`, `async void` — 0 результатів.
- `dotnet test` двічі підряд — той самий результат: кожен тест створює нове сховище, статичного стану немає.
- Усі асинхронні тести оголошені як `async Task`.

### Додаткові завдання
- **Потоковий імпорт** (`--stream`): `IBookStreamImporter.StreamAsync` повертає `IAsyncEnumerable<BookCopy>` через `JsonSerializer.DeserializeAsyncEnumerable`. Параметр токена позначено `[EnumeratorCancellation]`, тому токен, переданий через `.WithCancellation(ct)` у `await foreach` (`LendingService.ImportStreamAsync`), доходить до методу. Відмінність від `ImportAsync`: звичайний імпорт відхиляє файл цілком, потоковий додає записи по одному, тож ті, що до помилки, вже в сховищі.
- **Зв'язані токени** (`--timeout`): `ImportWithTimeoutAsync` / `ImportStreamWithTimeoutAsync` створюють токен таймауту й об'єднують його з токеном користувача через `CancellationTokenSource.CreateLinkedTokenSource`. Якщо скасування спричинив таймаут, а не користувач, `OperationCanceledException` перетворюється на `TimeoutException`. Cli розрізняє: «Імпорт скасовано.» (Ctrl+C) і «Імпорт перервано за таймаутом».
- **IProgress<int>** (`--progress`): `ImportAsync` повідомляє відсоток оброблених записів, лише коли він змінився. У Cli — `ConsoleProgress` (`src/Cli/ConsoleProgress.cs`), малює відсотки в одному рядку через `\r`. Використано власну реалізацію, а не `Progress<T>`: у консолі `Progress<T>` викликає обробник асинхронно в пулі потоків, і відсотки можуть прийти не по порядку або після підсумкового повідомлення. Для потокового імпорту прогресу немає: загальна кількість записів невідома до кінця файлу.
- **Покриття:**
  ```bash
  dotnet test --collect:"XPlat Code Coverage"
  reportgenerator -reports:"tests/Core.Tests/TestResults/*/coverage.cobertura.xml" -targetdir:coverage-report -reporttypes:"Html;TextSummary"
  ```
  Покриття рядків Core — 26.8% (195 з 726). Змінені в цій роботі класи покриті високо: `JsonBookImporter` — 92.5%, `LendingService` — 79.5%, `InMemoryBookStore` — 76.9%. Не покриті `FileBookStore` (працює з диском, тож це предмет інтеграційних, а не модульних тестів), демо й допоміжний код (`SampleData`, `Benchmark`, `EnvironmentInfo`) та старі імпортери `Core.Import`. Не покрита також доменна логіка видач (`Loan`, `LoanService`) і більшість `ReportService`: це не входило в обсяг роботи, але є кандидатом на тести надалі. `coverage-report/` додано в `.gitignore`.

## Лабораторна 9 — Шляхи, JSON, конфігурація, secrets

Мета: прибрати з коду жорстко задані шляхи, винести налаштування в `appsettings.json` з можливістю перевизначення змінними середовища, а секрети зберігати поза репозиторієм.

### Шляхи
- Літералів шляхів у `src` немає: `git grep -n -F "C:\" src`, `"/home/"`, `"\\"` — 0 результатів.
- Шляхи будуються через `Path.Combine`, а не конкатенацію з `"\"`: роздільник каталогів залежить від ОС (`\` у Windows, `/` у Linux/macOS).
- Базовий каталог — `AppContext.BaseDirectory` (каталог зі збіркою `Cli.dll`), а не `Environment.CurrentDirectory`, який залежить від того, звідки запущено процес.
- `StorageOptions.ResolveFullPath(baseDir)`: абсолютний `DataPath` береться як є, відносний — від `baseDir`; каталог створюється `Directory.CreateDirectory`.
- `StoreFactory.Create(args, dataFile)` отримує готовий шлях з конфігурації; у `FileBookStore` і фабриці шляхів немає.

Перевірка переносимості — в усіх запусках `Файл даних` однаковий (`src\Cli\bin\Debug\net10.0\data\catalog.json`):
```bash
dotnet run --project src/Cli -- report-sum      # з кореня solution
cd src/Cli && dotnet run -- report-sum          # з src/Cli
```

### Конфігурація
- **Пакети** встановлено лише в `src/Cli` (точка входу): `Microsoft.Extensions.Configuration`, `.Json`, `.EnvironmentVariables`, `.Binder`, `.UserSecrets`, `.CommandLine`. Core про файли конфігурації й user-secrets не знає.
- **AppOptions** (`src/Core/Configuration/AppOptions.cs`) — класи `AppOptions`, `LoggingOptions`, `StorageOptions`, `LibraryOptions` без залежностей від пакетів. Біндинг: `config.Get<AppOptions>()` зіставляє ключі з назвами властивостей (`Storage:DataPath` → `Storage.DataPath`).
- `appsettings*.json` копіюються у вихідний каталог через `<None Update="appsettings*.json">` з `CopyToOutputDirectory` у `Cli.csproj`; `SetBasePath(AppContext.BaseDirectory)`.

**Порядок джерел** (пізніше перекриває раніше):
```
appsettings.json → appsettings.{Environment}.json → user-secrets → змінні CROSSAPP_* → CROSSAPP_DATA_PATH
```

### Профілі
| Профіль | Файли | Рівень логу | Розташування даних |
|---------|-------|-------------|--------------------|
| базовий | `appsettings.json` | `Information` | `bin/Debug/net10.0/data/catalog.json` |
| Development | `appsettings.json` + `appsettings.Development.json` | `Debug` | те саме |

Профіль вибирається змінною `CROSSAPP_ENVIRONMENT` (за замовчуванням `Development`). Файл профілю містить лише ключі, що відрізняються від базового `appsettings.json`.

### Ключі конфігурації
| Ключ | За замовчуванням | Змінна середовища | Призначення |
|------|------------------|-------------------|-------------|
| `EnvironmentName` | `Development` | `CROSSAPP_EnvironmentName` | назва профілю у виводі |
| `Logging:MinimumLevel` | `Information` (`Debug` у Development) | `CROSSAPP_Logging__MinimumLevel` | рівень логу |
| `Storage:DataPath` | `data` | `CROSSAPP_Storage__DataPath` або `CROSSAPP_DATA_PATH` | каталог даних |
| `Storage:FileName` | `catalog.json` | `CROSSAPP_Storage__FileName` | ім'я файлу каталогу |
| `Library:LoanDays` | `14` | `CROSSAPP_Library__LoanDays` | термін видачі (ключ варіанту «Бібліотека») |
| `Integration:ApiKey` | — (лише user-secrets / env) | `CROSSAPP_Integration__ApiKey` | навчальний secret |

У змінних середовища двокрапка вкладеного ключа замінюється подвійним підкресленням, префікс `CROSSAPP_` відрізається провайдером (`AddEnvironmentVariables(prefix: "CROSSAPP_")`). Проста змінна `CROSSAPP_DATA_PATH` читається окремо й має найвищий пріоритет.

### Приклади змінних середовища
PowerShell (діють лише в поточному вікні терміналу):
```powershell
$env:CROSSAPP_DATA_PATH = "$env:TEMP\crossapp-data"
$env:CROSSAPP_Logging__MinimumLevel = "Warning"
dotnet run --project src/Cli -- report-sum
Remove-Item Env:CROSSAPP_*
```

bash (лише для однієї команди):
```bash
CROSSAPP_DATA_PATH=/tmp/crossapp-data dotnet run --project src/Cli -- report-sum
CROSSAPP_Logging__MinimumLevel=Warning dotnet run --project src/Cli -- report-sum
```

Вивід до і після зміни `CROSSAPP_DATA_PATH`:
```
Файл даних : C:\Users\Inna\...\CrossApp\src\Cli\bin\Debug\net10.0\data\catalog.json
Файл даних : C:\Users\Inna\AppData\Local\Temp\crossapp-data\catalog.json
```

### Secrets
Secret — навчальний рядок `Integration:ApiKey`. У консоль друкується лише факт наявності й довжина.
```bash
dotnet user-secrets init --project src/Cli          # дописує <UserSecretsId> у Cli.csproj (GUID комітиться)
dotnet user-secrets list --project src/Cli          # показує Integration:ApiKey
```
Значення ключа задається командою `dotnet user-secrets set "Integration:ApiKey" ... --project src/Cli` і в репозиторії не зберігається.
- Розташування `secrets.json`: Windows — `%APPDATA%\Microsoft\UserSecrets\b653d233-dd22-47c0-85dc-4c0fb8d057e5\secrets.json`; Linux/macOS — `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`.
- Файл лежить поза репозиторієм і не шифрується, тому user-secrets — лише для локальної розробки. На сервері — змінна `CROSSAPP_Integration__ApiKey` або сховище секретів.
- Перевірка: `git status` після `set` не містить `secrets.json`; пошук значення ключа через `git grep` — 0 результатів.
```
ApiKey     : присутній, довжина 13     # з user-secrets
ApiKey     : присутній, довжина 8      # CROSSAPP_Integration__ApiKey=from-env перекрила secret
```

### FileBookStore
- JSON-масив `BookDto` (`id`, `isbn`, `title`, `year`, `author`, `isIssued`). `BookCopy.FromDto` під час читання повторно перевіряє інваріанти.
- `JsonSerializerOptions`: `PropertyNamingPolicy = CamelCase`, `PropertyNameCaseInsensitive = true` (старий файл з `Id`, `Isbn` теж читається), `Encoder = UnsafeRelaxedJsonEscaping` (кирилиця без `\u0426`), `WriteIndented = true`.
- **Атомарний запис:** дані пишуться в `catalog.json.tmp`, потім `File.Move(tmp, path, overwrite: true)`. Якщо програма впаде посеред запису, зіпсується лише `.tmp`, основний файл лишиться цілим.

```json
{
  "id": "e79602b7",
  "isbn": "978-617-585-111-2",
  "title": "Intermezzo",
  "year": 2020,
  "author": "Михайло Коцюбинський",
  "isIssued": false
}
```