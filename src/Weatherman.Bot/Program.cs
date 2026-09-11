using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Weatherman.Bot;
using Weatherman.Bot.Services;
using Weatherman.Bot.Data;
using Geo.Extensions.DependencyInjection;
using DarkSky.Services;

// Avoid slow thread injection delaying interaction defers past Discord's 3s window.
ThreadPool.SetMinThreads(Math.Max(Environment.ProcessorCount * 4, 16), Math.Max(Environment.ProcessorCount * 4, 16));

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true)
    .AddEnvironmentVariables();

// Logging
builder.Logging
    .ClearProviders()
    .AddBotLogging(builder.Environment, builder.Configuration);

// A Discord gateway hiccup can throw inside a DiscordClientService. The default is to stop the
// host, which takes the bot down and loses anything held in memory; log and keep running instead.
builder.Services.Configure<HostOptions>(options =>
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

// Bot Configuration
builder.Services.Configure<BotConfiguration>(builder.Configuration);

var botConfiguration = builder.Configuration.Get<BotConfiguration>();

// Services
builder.Services.AddHereGeocoding()
    .AddKey(botConfiguration.HereApiKey);

builder.Services
    .AddCache(builder.Configuration)
    .AddDiscord()

    .AddTransient(sp => new DarkSkyService(
        botConfiguration.PirateWeatherKey,
        baseUri: new Uri(Constants.PirateWeatherApi),
        jsonSerializerService: new DarkSkyJsonSerializerService()))

    .AddSingleton<LocationService>()
    .AddSingleton<WeatherService>()
    .AddSingleton<HomeService>()
    .AddSingleton<StatsWriter>()

    .AddSingleton<DbContextHelper>()
    .AddDbContext<BotDbContext>();

// Build and run
var app = builder.Build();
await app.RunAsync();
