using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.MemoryStorage;
using RagAI_v2.MemoryDataBase.Postgres;
using CustomPostgresConfig = RagAI_v2.MemoryDataBase.Postgres.CustomPostgresConfig;

#pragma warning disable IDE0130 // reduce number of "using" statements
// ReSharper disable once CheckNamespace - reduce number of "using" statements
namespace RagAI_v2.Extensions;

/// <summary>
/// Extension de constructeur de Kernel Memory pour ajouter le connecteur Postgres en tant que base de données mémoire.
/// Kernel Memory builder extensions
/// </summary>
public static partial class KernelMemoryBuilderExtensions
{
    /// <summary>
    /// Kernel Memory Builder extension method to add Postgres memory connector.
    /// Méthode d'extension du Kernel Memory Builder pour ajouter le connecteur mémoire Postgres.
    /// </summary>
    /// <param name="builder">KM builder instance</param>
    /// <param name="config">Postgres configuration</param>
    public static IKernelMemoryBuilder WithCustomPostgresMemoryDb(this IKernelMemoryBuilder builder, CustomPostgresConfig config)
    {
        builder.Services.AddPostgresAsMemoryDb(config);
        return builder;
    }

    /// <summary>
    /// Kernel Memory Builder extension method to add Postgres memory connector.
    /// Méthode d'extension du Kernel Memory Builder pour ajouter le connecteur mémoire Postgres.
    /// </summary>
    /// <param name="builder">KM builder instance</param>
    /// <param name="connString">Postgres connection string</param>
    public static IKernelMemoryBuilder WithCustomPostgresMemoryDb(this IKernelMemoryBuilder builder, string connString)
    {
        builder.Services.AddPostgresAsMemoryDb(connString);
        return builder;
    }
}

/// <summary>
/// .NET IServiceCollection dependency injection extensions.
/// </summary>
public static partial class DependencyInjection
{
    /// <summary>
    /// Inject Postgres as the default implementation of IMemoryDb
    /// Injecte Postgres comme implémentation par défaut de IMemoryDb
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="config">Postgres configuration</param>
    public static IServiceCollection AddPostgresAsMemoryDb(this IServiceCollection services, CustomPostgresConfig config)
    {
        return services
            .AddSingleton<CustomPostgresConfig>(config)
            .AddSingleton<IMemoryDb, PostgresService>();
    }

    /// <summary>
    /// Inject Postgres as the default implementation of IMemoryDb
    /// Injecte Postgres comme implémentation par défaut de IMemoryDb
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="connString">Postgres connection string</param>
    public static IServiceCollection AddPostgresAsMemoryDb(this IServiceCollection services, string connString)
    {
        var config = new PostgresConfig { ConnectionString = connString };
        return services.AddPostgresAsMemoryDb(config);
    }
}
