using Microsoft.Extensions.DependencyInjection;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Infrastructure.Shared.InMemory;

public static class ServiceCollectionExtensions
{
    /// <summary>Registra los puertos de persistencia con la implementación en memoria (singleton).</summary>
    public static IServiceCollection AddInMemoryPersistence(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryDatabase>();
        services.AddSingleton<ITaskRepository, InMemoryTaskRepository>();
        services.AddSingleton<IDayRepository, InMemoryDayRepository>();
        services.AddSingleton<IConfigRepository, InMemoryConfigRepository>();
        services.AddSingleton<ISettingsStore, InMemorySettingsStore>();
        services.AddSingleton<IUnitOfWork, InMemoryUnitOfWork>();
        return services;
    }
}
