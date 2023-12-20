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
            var containerBuilder = Agent.Config.ContainerBuilder.Build(); 
            var container = containerBuilder.Build();

            using (var scope = container.BeginLifetimeScope())
            {
                var agent = scope.Resolve<IAgent>();
                await agent.Start();
            }
        }

    }
}
