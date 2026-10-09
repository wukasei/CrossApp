using Core;
using Core.Abstractions;
using Core.Storage;

namespace Cli;

public static class StoreFactory
{
    public static IBookStore Create(string[] args)
    {
        IBookStore store = args.Contains("--file")
            ? new FileBookStore(Path.Combine(AppContext.BaseDirectory, "data", "catalog.json"))
            : new InMemoryBookStore(SampleData.Books());

        if (args.Contains("--cache"))
            store = new CachingBookStore(store);

        return store;
    }
}