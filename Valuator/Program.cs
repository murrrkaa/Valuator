namespace Valuator;
using RabbitMQ.Client;
using StackExchange.Redis;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authentication.Cookies;

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

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Login";
                options.AccessDeniedPath = "/Forbidden";
            });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("AuthorOnly", policy => policy.RequireAuthenticatedUser());
        });


        string redisPassword = Environment.GetEnvironmentVariable("REDIS_PASSWORD");
        string rabbitUser = Environment.GetEnvironmentVariable("RABBIT_USER");
        string rabbitPassword = Environment.GetEnvironmentVariable("RABBIT_PASSWORD");

        builder.Services.AddRazorPages();
        builder.Services.AddSingleton<IConnectionMultiplexer>((_) =>
        {
            var dbMain = Environment.GetEnvironmentVariable("DB_MAIN");
            var config = ConfigurationOptions.Parse(dbMain);
            config.Password = redisPassword;

            return ConnectionMultiplexer.Connect(config);
        });

        builder.Services.AddSingleton<IRegionDBProvider, RegionDBProvider>();
        var factory = new ConnectionFactory { HostName = "localhost",
            UserName = rabbitUser,
            Password = rabbitPassword
        };

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
        app.UseAuthentication();
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