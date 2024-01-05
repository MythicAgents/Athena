using System;
using System.Threading.Tasks;
using System.Diagnostics;
using Autofac;
using Agent.Interfaces;
using System.Reflection;

namespace Athena
{
    class Program
    {
        /// <summary>
        /// Main Loop (Async)
        /// </summary>
        static async Task Main()
        {
            Console.WriteLine("In Main.");
            var containerBuilder = Agent.Config.ContainerBuilder.Build(); 
            var container = containerBuilder.Build();

            using (var scope = container.BeginLifetimeScope())
            {
                Console.WriteLine("Resolving Scope");
                var agent = scope.Resolve<IAgent>();
                Console.WriteLine("Starting Agent.");
                await agent.Start();
                Console.WriteLine("Exiting.");
            }
        }

    }
}
