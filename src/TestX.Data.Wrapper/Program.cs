using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TestX.Data.Wrapper.Runners;

public class Program
{
    public static async Task Main(string[] args)
    {
        await Host.CreateDefaultBuilder(args)
            .UseWindowsService() // Configures the app to run as a Windows Service
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService<ReceiverHostedService>();
            })
            .Build()
            .RunAsync();
    }
}

public class ReceiverHostedService : BackgroundService
{
    private readonly Receiver _receiver;
    private readonly Wrapper _wrapper;

    public ReceiverHostedService()
    {
        _receiver = new Receiver();
        _wrapper = new Wrapper();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // await _receiver.InitializeDailyTask();
        await _wrapper.Start();
    }
}
//
//
// using System;
// using System.Threading;
// using System.Threading.Tasks;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.DependencyInjection;
// using Microsoft.Extensions.Hosting;
// using TestX.Data.Contexts;
// using TestX.Data.Wrapper.Runners;
//
// public class Program
// {
//     public static async Task Main(string[] args)
//     {
//         var host = Host.CreateDefaultBuilder(args)
//             .UseWindowsService() // Настраивает хост для работы как Windows Service
//             .ConfigureServices((context, services) =>
//             {
//                 services.AddDbContext<DataBaseContext>(options =>
//                     options.UseSqlServer("data source=(local);initial catalog=DataX;persist security info=True;user id=sa;password=eso8Yv0#;MultipleActiveResultSets=True;App=EntityFramework;TrustServerCertificate=True"));
//
//                 services.AddScoped<Wrapper>();
//                 services.AddScoped<Service>(); // Зарегистрируйте службу
//                 services.AddHostedService<Service>(); // Зарегистрируйте службу
//             })
//             .Build();
//
//         await host.RunAsync();
//     }
// }
//
// // Класс службы
// public class Service : BackgroundService
// {
//     private readonly IServiceProvider _serviceProvider;
//
//     public Service(IServiceProvider serviceProvider)
//     {
//         _serviceProvider = serviceProvider;
//     }
//
//     protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//     {
//         try
//         {
//             using var scope = _serviceProvider.CreateScope();
//             var receiver = scope.ServiceProvider.GetRequiredService<Wrapper>();
//             await receiver.Start();
//         }
//         catch (Exception ex)
//         {
//             Console.WriteLine($"Error in service execution: {ex}");
//         }
//     }
// }
