using FluentAssertions;
using HO.Application.Interfaces;
using HO.Application.Queries.Dashboard;
using HO.Domain.Entities;
using HO.Domain.Enums;
using Moq;
using Xunit;

namespace HO.Application.Tests;

public class DashboardSummaryQueryTests
{
    private readonly Mock<IFYJobRepository>    _fyJobRepo    = new();
    private readonly Mock<IStoreRepository>    _storeRepo    = new();
    private readonly Mock<ITerminalRepository> _terminalRepo = new();
    private readonly Mock<ICommandRepository>  _commandRepo  = new();

    private GetDashboardSummaryQueryHandler BuildHandler() =>
        new(_fyJobRepo.Object, _storeRepo.Object,
            _terminalRepo.Object, _commandRepo.Object);

    private void SetupDefaults()
    {
        _commandRepo.Setup(r => r.GetRecentAsync(1000, default))
                    .ReturnsAsync(new List<Command>());
        _terminalRepo.Setup(r => r.GetOfflineTerminalsAsync(It.IsAny<TimeSpan>(), default))
                     .ReturnsAsync(new List<Terminal>());
        _fyJobRepo.Setup(r => r.GetActiveJobAsync(default))
                  .ReturnsAsync((FinancialYearJob?)null);
    }

    [Fact]
    public async Task Handle_ReturnsCorrectCounts_WhenStoresExist()
    {
        SetupDefaults();
        _storeRepo.Setup(r => r.GetAllAsync(null, default)).ReturnsAsync(new List<Store>
        {
            new() { StoreId = Guid.NewGuid(), FYCloseStatus = FYCloseStatus.Completed },
            new() { StoreId = Guid.NewGuid(), FYCloseStatus = FYCloseStatus.Completed },
            new() { StoreId = Guid.NewGuid(), FYCloseStatus = FYCloseStatus.Failed    },
            new() { StoreId = Guid.NewGuid(), FYCloseStatus = FYCloseStatus.Pending   },
            new() { StoreId = Guid.NewGuid(), FYCloseStatus = FYCloseStatus.Offline   },
        });

        var result = await BuildHandler().Handle(new GetDashboardSummaryQuery(null), default);

        result.TotalStores.Should().Be(5);
        result.Completed.Should().Be(2);
        result.Failed.Should().Be(1);
        result.CompletionPct.Should().Be(40.0);
    }

    [Fact]
    public async Task Handle_ReturnsZeroCompletion_WhenNoStores()
    {
        SetupDefaults();
        _storeRepo.Setup(r => r.GetAllAsync(null, default)).ReturnsAsync(new List<Store>());

        var result = await BuildHandler().Handle(new GetDashboardSummaryQuery(null), default);

        result.TotalStores.Should().Be(0);
        result.CompletionPct.Should().Be(0);
    }

    [Fact]
    public async Task Handle_RunningCount_ReflectsActiveCommands()
    {
        SetupDefaults();
        var storeId = Guid.NewGuid();
        _storeRepo.Setup(r => r.GetAllAsync(null, default))
                  .ReturnsAsync(new List<Store> { new() { StoreId = storeId, FYCloseStatus = FYCloseStatus.Pending } });
        _commandRepo.Setup(r => r.GetRecentAsync(1000, default))
                    .ReturnsAsync(new List<Command> { new() { StoreId = storeId, Status = CommandStatus.Running } });

        var result = await BuildHandler().Handle(new GetDashboardSummaryQuery(null), default);

        result.Running.Should().Be(1);
        result.Pending.Should().Be(0); // excluded because it's running
    }

    [Fact]
    public async Task Handle_OfflineCount_ReflectsOfflineTerminals()
    {
        SetupDefaults();
        var storeId = Guid.NewGuid();
        _storeRepo.Setup(r => r.GetAllAsync(null, default))
                  .ReturnsAsync(new List<Store> { new() { StoreId = storeId, FYCloseStatus = FYCloseStatus.Pending } });
        _terminalRepo.Setup(r => r.GetOfflineTerminalsAsync(It.IsAny<TimeSpan>(), default))
                     .ReturnsAsync(new List<Terminal> { new() { StoreId = storeId } });

        var result = await BuildHandler().Handle(new GetDashboardSummaryQuery(null), default);

        result.Offline.Should().Be(1);
        result.Pending.Should().Be(0); // excluded because it's offline
    }
}
