#if WINDOWS_SERVICE
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Agent.Interfaces;
using Agent.Config;
using Agent;


public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

        //
        // Start the agent
        //
        _ = Task.Run(async () =>
        {        
            var containerBuilder = Agent.Config.ContainerBuilder.Build();
            var container = containerBuilder.Build();
            using (var scope = container.BeginLifetimeScope())
            {
                var agent = scope.Resolve<IAgent>();
                _ = agent.Start();
            }        

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker working at: {time}", DateTimeOffset.Now);
                await Task.Delay(1000, stoppingToken); // Simulate work
            }
        });

        return Task.CompletedTask; // Return a completed task
    }
}

#endif