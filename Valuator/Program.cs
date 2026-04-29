namespace Valuator;
using RabbitMQ.Client;
using StackExchange.Redis;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.StackExchangeRedis;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorPages();

        builder.Services.AddSingleton<IConnectionMultiplexer>((_) =>
            ConnectionMultiplexer.Connect("localhost:6379"));
        builder.Services.AddSingleton<IConnectionFactory>(new ConnectionFactory { HostName = "localhost" });
        builder.Services.AddSignalR()
            .AddStackExchangeRedis("localhost:6379", options => {
                    options.Configuration.ChannelPrefix = "Valuator_SignalR_Backplane";
                });
        builder.Services.AddHostedService<RankNotifier>();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
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
}
