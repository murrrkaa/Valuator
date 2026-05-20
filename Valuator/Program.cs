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

    public static async Task Main(string[] args)
    {

        var builder = WebApplication.CreateBuilder(args);

        ConfigureSecurity(builder);

        await ConfigureInfrastructure(builder);

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

    private static async Task ConfigureInfrastructure(WebApplicationBuilder builder)
    {
        string redisPassword = Environment.GetEnvironmentVariable("REDIS_PASSWORD_MAIN") ?? "";
        string rabbitUser = Environment.GetEnvironmentVariable("RABBIT_USER") ?? "";
        string rabbitPassword = Environment.GetEnvironmentVariable("RABBIT_PASSWORD") ?? "";
        string dbMain = Environment.GetEnvironmentVariable("DB_MAIN") ?? "localhost:6000";

        builder.Services.AddRazorPages();
        builder.Services.AddSingleton<IConnectionMultiplexer>((_) =>
        {
            var config = ConfigurationOptions.Parse(dbMain);
            config.Password = redisPassword;
            return ConnectionMultiplexer.Connect(config);
        });

        builder.Services.AddSingleton<IRegionDBProvider, RegionDBProvider>();

        var factory = new ConnectionFactory
        {
            HostName = "localhost",
            UserName = rabbitUser,
            Password = rabbitPassword
        };

        var rabbitConnection = await factory.CreateConnectionAsync();

        builder.Services.AddSingleton<IConnection>(rabbitConnection);
        builder.Services.AddSingleton<IConnectionFactory>(factory);

        await SetupRabbitMqTopology(rabbitConnection);
    }

    private static void ConfigureSecurity(WebApplicationBuilder builder)
    {
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Login";
                options.AccessDeniedPath = "/ClientError/403";
            });

        builder.Services.AddAuthorization();
    }

    private static async Task SetupRabbitMqTopology(IConnection connection)
    {
        using var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(
            ExchangeNameRank, 
            ExchangeType.Direct
        );
        await channel.QueueDeclareAsync(
            QueueName, 
            durable: true, 
            exclusive: false, 
            autoDelete: false
        );
        await channel.QueueBindAsync(
            QueueName, 
            ExchangeNameRank, 
            RoutingKey
        );
        await channel.ExchangeDeclareAsync(
            ExchangeNameEvents, 
            ExchangeType.Fanout
        );
    }
}