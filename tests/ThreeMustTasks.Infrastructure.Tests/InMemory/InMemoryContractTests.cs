using Microsoft.Extensions.DependencyInjection;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Infrastructure.Shared.InMemory;
using ThreeMustTasks.Infrastructure.Tests.Contracts;

namespace ThreeMustTasks.Infrastructure.Tests.InMemory;

internal static class InMemoryFactory
{
    public static PersistenceSet Create()
    {
        var sp = new ServiceCollection().AddInMemoryPersistence().BuildServiceProvider();
        return new PersistenceSet(
            sp.GetRequiredService<ITaskRepository>(),
            sp.GetRequiredService<IDayRepository>(),
            sp.GetRequiredService<IConfigRepository>(),
            sp.GetRequiredService<ISettingsStore>(),
            sp.GetRequiredService<IUnitOfWork>());
    }
}

public sealed class InMemoryTaskRepositoryTests : TaskRepositoryContract
{
    protected override PersistenceSet Create() => InMemoryFactory.Create();
}

public sealed class InMemoryDayRepositoryTests : DayRepositoryContract
{
    protected override PersistenceSet Create() => InMemoryFactory.Create();
}

public sealed class InMemoryConfigRepositoryTests : ConfigRepositoryContract
{
    protected override PersistenceSet Create() => InMemoryFactory.Create();
}

public sealed class InMemorySettingsStoreTests : SettingsStoreContract
{
    protected override PersistenceSet Create() => InMemoryFactory.Create();
}

public sealed class InMemoryRegistrationTests
{
    [Fact]
    public void Stores_share_one_database_per_container()
    {
        var sp = new ServiceCollection().AddInMemoryPersistence().BuildServiceProvider();
        Assert.Same(sp.GetRequiredService<ITaskRepository>(), sp.GetRequiredService<ITaskRepository>());
    }
}
