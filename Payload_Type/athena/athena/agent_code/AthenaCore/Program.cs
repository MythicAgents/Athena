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
#if WINDOWS_SERVICE
            // Run as a Windows Service
            Console.WriteLine("Starting as a Windows Service...");
            IHost host = Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .ConfigureServices(services =>
                {
                    services.AddHostedService<Worker>();
                })
                .Build();

            await host.RunAsync();
#else            
            var containerBuilder = Agent.Config.ContainerBuilder.Build();
            var container = containerBuilder.Build();
            using (var scope = container.BeginLifetimeScope())
            {
                var agent = scope.Resolve<IAgent>();
                await agent.Start();
            }
#endif
        }
    }
}
