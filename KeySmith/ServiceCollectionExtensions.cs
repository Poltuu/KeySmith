using KeySmith.Internals.Locks;
using KeySmith.Internals.Scripts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System;

namespace KeySmith
{
    /// <summary>
    /// A set of extensions methods on <see cref="IServiceCollection"/>
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Add <see cref="ILockService"/> and <see cref="IMemoLockService"/> to the provided <paramref name="services"/>.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="redisConfiguration"><see cref="ConnectionMultiplexer"/> will be registered as a singleton using the provided options</param>
        /// <returns></returns>
        public static IServiceCollection AddKeySmith(this IServiceCollection services, string redisConfiguration)
            => services.AddKeySmithWithoutConnectionMultiplexer()
                .AddSingleton(p => ConnectionMultiplexer.Connect(ConfigurationOptions.Parse(redisConfiguration)));

        /// <summary>
        /// Add <see cref="ILockService"/> and <see cref="IMemoLockService"/> to the provided <paramref name="services"/>.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="options"><see cref="ConnectionMultiplexer"/> will be registered as a singleton using the provided options</param>
        /// <returns></returns>
        public static IServiceCollection AddKeySmith(this IServiceCollection services, ConfigurationOptions options)
            => services.AddKeySmithWithoutConnectionMultiplexer()
                .AddSingleton(p => ConnectionMultiplexer.Connect(options));

        /// <summary>
        /// Add <see cref="ILockService"/> and <see cref="IMemoLockService"/> to the provided <paramref name="services"/>.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="getConnection"><see cref="ConnectionMultiplexer"/> will be registered as a singleton using the provided callback</param>
        /// <returns></returns>
        public static IServiceCollection AddKeySmith(this IServiceCollection services, Func<IServiceProvider, ConnectionMultiplexer> getConnection)
            => services.AddKeySmithWithoutConnectionMultiplexer()
                .AddSingleton(getConnection);

        /// <summary>
        /// Add <see cref="ILockService"/> and <see cref="IMemoLockService"/> to the provided <paramref name="services"/>.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="options"><see cref="ConnectionMultiplexer"/> will be registered as a singleton using the provided options</param>
        /// <returns></returns>
        public static IServiceCollection AddKeySmith(this IServiceCollection services, IOptions<ConfigurationOptions> options)
            => services.AddKeySmithWithoutConnectionMultiplexer()
                .AddSingleton(p => ConnectionMultiplexer.Connect(options.Value));

        /// <summary>
        /// Add <see cref="ILockService"/> and <see cref="IMemoLockService"/> to the provided <paramref name="services"/>.
        /// <para></para>
        /// <see cref="ConnectionMultiplexer"/> needs to be injectable and configured on this <see cref="IServiceCollection"/>
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddKeySmithWithoutConnectionMultiplexer(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.TryAddSingleton<ILockService, LockService>();
            services.TryAddSingleton<IMemoLockService, MemoLockService>();

            return services
                .AddSingleton<IdentifierGenerator>()
                .AddSingleton<IScriptLibrary, ScriptLibrary>()
                .AddSingleton<IMemoScriptLibrary, MemoScriptLibrary>();
        }
    }
}