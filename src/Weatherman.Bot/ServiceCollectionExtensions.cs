using System.ComponentModel.DataAnnotations;
using Discord;
using Discord.Addons.Hosting;
using Discord.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Weatherman.Bot.Services;
using ZiggyCreatures.Caching.Fusion;

namespace Weatherman.Bot;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDiscord(this IServiceCollection services)
    {
        services.AddOptionsWithValidateOnStart<BotConfiguration>()
            .ValidateDataAnnotations();

        services.AddDiscordHost((config, sp) =>
        {
            var opts = sp.GetRequiredService<IOptions<BotConfiguration>>().Value;

            config.Token = opts.DiscordToken;

            config.SocketConfig = new()
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.DirectMessages,
                LogLevel = LogSeverity.Info,
                AlwaysDownloadUsers = false,
                UseInteractionSnowflakeDate = false
            };
        });

        services.AddCommandService((config, _) =>
        {
            config.DefaultRunMode = RunMode.Async;
            config.CaseSensitiveCommands = false;
        });

        services.AddInteractionService((config, sp) =>
        {
            var opts = sp.GetRequiredService<IOptions<BotConfiguration>>().Value;

            config.LogLevel = LogSeverity.Info;
            config.UseCompiledLambda = true;
            config.DefaultRunMode = Discord.Interactions.RunMode.Async;
        });

        services
            .AddHostedService<InteractionHandler>()
            .AddHostedService<BotStatusService>();

        return services;
    }

    public static IServiceCollection AddCache(this IServiceCollection services, IConfiguration configuration)
    {
        var cacheOptions = services.ApplyValidatedOptions<BotConfiguration>(configuration);

        var cacheBuilder = services
            .AddFusionCache()
            .WithOptions(options =>
            {
                options.CacheKeyPrefix = typeof(ServiceCollectionExtensions).Assembly.GetName().Name;
            });

        if (!string.IsNullOrWhiteSpace(cacheOptions.RedisAddress))
        {
            cacheBuilder.WithStackExchangeRedisBackplane(options =>
            {
                options.Configuration = cacheOptions.RedisAddress;
            });
        }

        return services;
    }

    public static T ApplyValidatedOptions<T>(this IServiceCollection services, IConfiguration configuration)
        where T : class, new()
    {
        services.AddOptionsWithValidateOnStart<T>()
            .ValidateDataAnnotations();

        // A section that is absent entirely binds to null; validate a default instance so the
        // failure names the missing settings rather than surfacing as a null reference.
        var options = configuration.Get<T>() ?? new T();

        Validate(options);

        return options;
    }

    private static void Validate<T>(T options)
        where T : class
    {
        var results = new List<ValidationResult>();

        if (Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true))
        {
            return;
        }

        var failures = results
            .Select(result => string.Join(", ", result.MemberNames) is { Length: > 0 } members
                ? $"{members} - {result.ErrorMessage}"
                : $"{result.ErrorMessage}")
            .ToList();

        throw new OptionsValidationException(null, typeof(T), failures);
    }
}
