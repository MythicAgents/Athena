using Autofac;
using Agent.Interfaces;
using Agent.Config;
using Agent;

#if WINDOWS_SERVICE
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
#endif

namespace Athena
{
    class Program
    {

        /// <summary>
        /// Main Loop (Async)
        /// </summary>
        static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
            {
                Console.WriteLine($"[UnhandledException] {eventArgs.ExceptionObject}");
            };
            TaskScheduler.UnobservedTaskException += (sender, eventArgs) =>
            {
                Console.WriteLine($"[UnobservedTaskException] {eventArgs.Exception}");
                eventArgs.SetObserved();
            };

#if WINDOWS_SERVICE
            // Run as a Windows Service
            Console.WriteLine("Starting as a Windows Service...");
            try
            {
                IHost host = Host.CreateDefaultBuilder(args)
                    .UseWindowsService()
                    .ConfigureServices(services =>
                    {
                        services.AddHostedService<Worker>();
                    })
                    .Build();

                await host.RunAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Service Error] {ex}");
            }
#else
            while (true)
            {
                try
                {
                    var containerBuilder = Agent.Config.ContainerBuilder.Build();
                    var container = containerBuilder.Build();
                    using var scope = container.BeginLifetimeScope();
                    var agent = scope.Resolve<IAgent>();
                    await agent.Start().ConfigureAwait(false);
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Agent Error] {ex}");
                    await Task.Delay(5000).ConfigureAwait(false);
                }
            }
#endif
        }
    }
}
