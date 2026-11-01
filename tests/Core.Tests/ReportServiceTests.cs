using Core.Reports;
using Xunit;

namespace Core.Tests;

public class ReportServiceTests
{
    [Theory] // 5) звіт на кількох наборах вхідних даних
    [InlineData(0, 0)]
    [InlineData(3, 3)]
    [InlineData(10, 5)]   // просимо більше, ніж є, — отримуємо всі 5
    public void TopNewest_ReturnsAtMostN(int n, int expected) =>
        Assert.Equal(expected, ReportService.TopNewest(TestData.Items(), n).Count);

    [Fact] // 6) детермінований топ-N: при однаковому році — порядок за Id
    public void TopNewest_IsDeterministic() =>
        Assert.Equal(new[] { "T-05", "T-02", "T-03" },
                     ReportService.TopNewest(TestData.Items(), 3).Select(l => l.Id));
}