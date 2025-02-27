using Common.Redis;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RedisStream.Reader;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);


builder.Services.Configure<Connections>(builder.Configuration.GetSection("Connections"));

builder.Services.AddLogging(configure =>
{
    configure.AddConsole();
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("redisstreamreader.worker"))
    .WithTracing(tracing =>
    {
        tracing
            .SetSampler<AlwaysOnSampler>()
            .AddHttpClientInstrumentation()
            .AddRedisInstrumentation()
            .AddOtlpExporter();
        
        tracing.ConfigureRedisInstrumentation((services, configure) =>
        {
            var nonKeyedLazyMultiplexer =
                services.GetRequiredService<Lazy<IConnectionMultiplexer>>();
            configure.AddConnection("Multiplexer", nonKeyedLazyMultiplexer.Value);
        });
    });

builder.Services.TryAddSingleton<Lazy<IConnectionMultiplexer>>(provider =>
{
    var connectionString = provider.GetRequiredService<IOptions<Connections>>().Value.Redis.ConnectionString;
    return new Lazy<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(connectionString));
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.TryAddSingleton<IRedisStreamsService, RedisStreamsService>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();