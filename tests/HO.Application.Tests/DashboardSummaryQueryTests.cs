using FluentAssertions;
using HO.Application.DTOs;
using HO.Application.Interfaces;
using HO.Application.Queries.Dashboard;
using HO.Domain.Entities;
using HO.Domain.Enums;
using Moq;
using Xunit;

namespace HO.Application.Tests;

public class DashboardSummaryQueryTests
{
    private readonly Mock<IFYJobRepository> _fyJobRepoMock = new();
    private readonly Mock<IStoreRepository> _storeRepoMock = new();

    [Fact]
    public async Task Handle_ReturnsCorrectSummary_WhenStoresExist()
    {
        // Arrange
        var stores = new List<Store>
        {
            new() { FYCloseStatus = FYCloseStatus.Completed },
            new() { FYCloseStatus = FYCloseStatus.Completed },
            new() { FYCloseStatus = FYCloseStatus.Failed },
            new() { FYCloseStatus = FYCloseStatus.Pending },
            new() { FYCloseStatus = FYCloseStatus.Offline },
        };

        _storeRepoMock.Setup(r => r.GetAllAsync(null, default))
            .ReturnsAsync(stores);
        _fyJobRepoMock.Setup(r => r.GetActiveJobAsync(default))
            .ReturnsAsync((FinancialYearJob?)null);

        var handler = new GetDashboardSummaryQueryHandler(_fyJobRepoMock.Object, _storeRepoMock.Object);

        // Act
        var result = await handler.Handle(new GetDashboardSummaryQuery(null), default);

        // Assert
        result.TotalStores.Should().Be(5);
        result.Completed.Should().Be(2);
        result.Failed.Should().Be(1);
        result.Pending.Should().Be(1);
        result.Offline.Should().Be(1);
        result.CompletionPct.Should().Be(40.0);
    }

    [Fact]
    public async Task Handle_ReturnsZeroCompletion_WhenNoStores()
    {
        _storeRepoMock.Setup(r => r.GetAllAsync(null, default)).ReturnsAsync(new List<Store>());
        _fyJobRepoMock.Setup(r => r.GetActiveJobAsync(default)).ReturnsAsync((FinancialYearJob?)null);

        var handler = new GetDashboardSummaryQueryHandler(_fyJobRepoMock.Object, _storeRepoMock.Object);
        var result = await handler.Handle(new GetDashboardSummaryQuery(null), default);

        result.TotalStores.Should().Be(0);
        result.CompletionPct.Should().Be(0);
    }
}
