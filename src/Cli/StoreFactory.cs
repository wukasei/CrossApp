using Core;
using Core.Abstractions;
using Core.Storage;

namespace Cli;

public static class StoreFactory
{
    public static IBookStore Create(string[] args, string dataFile)
    {
        IBookStore store;
        if (args.Contains("--file"))
        {
            var fileStore = new FileBookStore(dataFile);
            fileStore.List();   // читаємо файл одразу: зіпсований файл виявиться тут, а не посеред роботи
            store = fileStore;
        }
        else
        {
            store = new InMemoryBookStore(SampleData.Books());
        }

        if (args.Contains("--cache"))
            store = new CachingBookStore(store);

        return store;
    }
}