using Autofac;
using Agent.Interfaces;
using Agent.Config;
using Agent;

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
