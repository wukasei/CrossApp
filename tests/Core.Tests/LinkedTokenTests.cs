using Core.Abstractions;
using Core.Domain;
using Core.Services;
using Core.Storage;
using Xunit;

namespace Core.Tests;

public class LinkedTokenTests
{
    // Імпортер, який «висить» вічно, поки його не скасують
    private sealed class HangingImporter : IBookImporter
    {
        public async Task<IReadOnlyList<BookCopy>> LoadAsync(string path, CancellationToken ct = default)
        {
            await Task.Delay(Timeout.Infinite, ct);
            return [];
        }
    }

    [Fact] // 12) додаткове 2: доводимо, що спрацював саме таймаут
    public async Task ImportWithTimeout_WhenTimeoutExpires_ThrowsTimeoutException()
    {
        var service = new LendingService(new InMemoryBookStore());
        using var user = new CancellationTokenSource();

        var ex = await Assert.ThrowsAsync<TimeoutException>(
            () => service.ImportWithTimeoutAsync(new HangingImporter(), "x",
                                                 TimeSpan.FromMilliseconds(50), user.Token));

        Assert.False(user.IsCancellationRequested);                       // користувач нічого не скасовував
        Assert.IsAssignableFrom<OperationCanceledException>(ex.InnerException);   // причина збережена
    }

    [Fact] // 13) додаткове 2: якщо скасував користувач — це не таймаут
    public async Task ImportWithTimeout_WhenUserCancels_ThrowsOperationCanceled()
    {
        var service = new LendingService(new InMemoryBookStore());
        using var user = new CancellationTokenSource();
        user.CancelAfter(50);   // «користувач натиснув Ctrl+C» через 50 мс

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ImportWithTimeoutAsync(new HangingImporter(), "x",
                                                 TimeSpan.FromMinutes(1), user.Token));
    }
}