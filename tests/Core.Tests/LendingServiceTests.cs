using Core.Domain;
using Core.Events;
using Core.Services;
using Core.Storage;
using Xunit;

namespace Core.Tests;

public class LendingServiceTests
{
    [Fact] // 1) інваріант: поганий аргумент → відмова, стан не змінився
    public void IssueCopy_WithEmptyReader_ThrowsArgumentException_AndKeepsState()
    {
        var store = new InMemoryBookStore(TestData.Items());
        var service = new LendingService(store);

        Assert.Throws<ArgumentException>(() => service.IssueCopy("T-01", "   "));

        Assert.Equal(CopyStatus.Available, store.GetById("T-01")!.Status);
    }

    [Fact] // 2) бізнес-правило: повторна видача → DomainException, події немає
    public void IssueCopy_WhenAlreadyIssued_ThrowsDomainException_AndRaisesNoEvent()
    {
        var store = new InMemoryBookStore(TestData.Items());
        var service = new LendingService(store);
        var events = new List<LibraryChangedEventArgs>();
        service.Changed += (_, e) => events.Add(e);

        var ex = Assert.Throws<DomainException>(() => service.IssueCopy("T-02", "R-9"));

        Assert.Contains("вже виданий", ex.Message);
        Assert.Empty(events);
        Assert.Equal(CopyStatus.Issued, store.GetById("T-02")!.Status);
    }

    [Fact] // 3) варіант «Бібліотека»: повернення невиданого примірника
    public void ReturnCopy_WhenNotIssued_ThrowsDomainException_AndKeepsState()
    {
        var store = new InMemoryBookStore(TestData.Items());
        var service = new LendingService(store);

        Assert.Throws<DomainException>(() => service.ReturnCopy("T-01"));

        Assert.Equal(CopyStatus.Available, store.GetById("T-01")!.Status);
    }

    [Fact] // 4) подія з тижня 6 як спосіб перевірки
    public void AddBook_RaisesChangedExactlyOnce()
    {
        var service = new LendingService(new InMemoryBookStore());
        var events = new List<LibraryChangedEventArgs>();
        service.Changed += (_, e) => events.Add(e);

        var copy = service.AddBook("978-0-00-000009-7", "Енеїда", 2018, "І. Котляревський");

        var single = Assert.Single(events);
        Assert.Equal(LibraryChangeKind.BookAdded, single.Kind);
        Assert.Equal(copy.Id, single.CopyId);
    }
}