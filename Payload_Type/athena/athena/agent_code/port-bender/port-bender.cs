using Agent.Interfaces;
using System.Text.Json;
using Agent.Models;
using System.Net.Sockets;
using System.Net;
using Agent.Utilities;
using port_bender;

namespace Agent
{
    public class Plugin : IPlugin
    {
        public string Name => "port-bender";
        private IMessageManager messageManager { get; set; }
        private bool running = false;
        private string start_task = String.Empty;
        public Plugin(IMessageManager messageManager, IAgentConfig config, ILogger logger, ITokenManager tokenManager, ISpawner spawner)
        {
            this.messageManager = messageManager;
        }

        public async Task Execute(ServerJob job)
        {
            PortBenderArgs args = JsonSerializer.Deserialize<PortBenderArgs>(job.task.parameters);
            if (running)
            {
                await this.Stop(job.task.id);
                return;
            }


            string host = args.destination.Split(':')[0];
            string sPort = args.destination.Split(':')[1];
            int port = 0;
            
            if(!int.TryParse(sPort, out port))
            {
                await messageManager.WriteLine($"Failed to get destination port.", start_task, true, "error");
                return;
            }

            new TcpForwarder().Start(
                new IPEndPoint(IPAddress.Any, args.port),
                new IPEndPoint(IPAddress.Parse(host), port));




           // await StartPortBenderAsync(args.port, host, port, job.cancellationtokensource, job.task.id);

        }
        //private async Task StartPortBenderAsync(int listenPort, string destinationHost, int destinationPort, CancellationTokenSource cts, string task_id)
        //{
        //    TcpListener listener = new TcpListener(IPAddress.Any, listenPort);
        //    listener.Start();
        //    start_task = task_id;
        //    await messageManager.WriteLine($"Port bender listening on port {listenPort}. Forwarding to {destinationHost}:{destinationPort}", task_id, false);

        //    while (!cts.IsCancellationRequested)
        //    {
        //        try
        //        {
        //            TcpClient client = await listener.AcceptTcpClientAsync();
        //            _ = HandleClientAsync(client, destinationHost, destinationPort);
        //        }
        //        catch (Exception ex)
        //        {
        //            await messageManager.WriteLine($"Error in server: {ex.Message}", start_task, true);
        //        }
        //    }
        //    await messageManager.WriteLine($"Server stopped.", start_task, true);
        //}

        //private async Task HandleClientAsync(TcpClient client, string destinationHost, int destinationPort)
        //{
        //    using (TcpClient destinationClient = new TcpClient())
        //    {
        //        await destinationClient.ConnectAsync(destinationHost, destinationPort);

        //        using (NetworkStream clientStream = client.GetStream())
        //        using (NetworkStream destinationStream = destinationClient.GetStream())
        //        {
        //            _ = Task.Run(async () =>
        //            {
        //                try
        //                {
        //                    await clientStream.CopyToAsync(destinationStream);
        //                }
        //                catch (Exception ex)
        //                {
        //                    await messageManager.WriteLine($"Error forwarding data to destination: {ex.Message}", start_task, false);
        //                }
        //                finally
        //                {
        //                    client.Close();
        //                    destinationClient.Close();
        //                }
        //            });

        //            _ = Task.Run(async () =>
        //            {
        //                try
        //                {
        //                    await destinationStream.CopyToAsync(clientStream);
        //                }
        //                catch (Exception ex)
        //                {
        //                    await messageManager.WriteLine($"Error forwarding data to client: {ex.Message}", start_task, false);
        //                }
        //                finally
        //                {
        //                    client.Close();
        //                    destinationClient.Close();
        //                }
        //            });
        //        }
        //    }
        //}
        //private async Task Stop(string task_id)
        //{
        //    if (!String.IsNullOrEmpty(start_task))
        //    {
        //        ServerJob job;

        //        if (messageManager.TryGetJob(start_task, out job))
        //        {
        //            job.cancellationtokensource.Cancel();
        //            await messageManager.WriteLine("Server tasked to exit.", task_id, true);
        //            return;
        //        }
        //        await messageManager.WriteLine("Couldn't find job.", task_id, true, "error");
        //        return;
        //    }

        //    await messageManager.WriteLine("No task_id specified, is the server running?", task_id, true, "error");
        //}
    }
}
