using RabbitMQ.Client;
using StackExchange.Redis;
namespace Valuator;
using RabbitMQ.Client;

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

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthorization();

        app.MapRazorPages();

        app.Run();
    }
}
