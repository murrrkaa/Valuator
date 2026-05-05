namespace Valuator;
using RabbitMQ.Client;
using StackExchange.Redis;
using Microsoft.AspNetCore.SignalR;

public class Program
{
    private const string ExchangeNameRank = "valuator.processing.rank";
    private const string ExchangeNameEvents = "valuator.processing.events";
    private const string QueueName = "valuator.processing.rank";
    private const string RoutingKey = "valuator.processing.rank";
    private const string ExchangeNameNotification = "valuator.processing.notification";

    public static async Task Main(string[] args)
    {

        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddRazorPages();
        builder.Services.AddSingleton<IConnectionMultiplexer>((_) =>
        {
            var dbMain = Environment.GetEnvironmentVariable("DB_MAIN") ?? "localhost:6000";
            return ConnectionMultiplexer.Connect(dbMain);
        });

        builder.Services.AddSingleton<IRegionDBProvider, RegionDBProvider>();
        var factory = new ConnectionFactory { HostName = "localhost" };

        var rabbitConnection = await factory.CreateConnectionAsync();

        builder.Services.AddSingleton<IConnection>(rabbitConnection);
        builder.Services.AddSingleton<IConnectionFactory>(factory);

        await SetupRabbitMqTopology(rabbitConnection);

        builder.Services.AddSignalR();
        builder.Services.AddHostedService<RankNotifier>();

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthorization();
        app.MapHub<RankHub>("/rankHub");
        app.MapRazorPages();
        app.Run();
    }

    private static async Task SetupRabbitMqTopology(IConnection connection)
    {
        using var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(ExchangeNameRank, ExchangeType.Direct);
        await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync(QueueName, ExchangeNameRank, RoutingKey);
        await channel.ExchangeDeclareAsync(ExchangeNameEvents, ExchangeType.Fanout);
        await channel.ExchangeDeclareAsync(ExchangeNameNotification, ExchangeType.Fanout);
    }
}