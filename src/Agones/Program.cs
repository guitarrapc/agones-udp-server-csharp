using System;
using System.Threading.Tasks;
using MicroBatchFramework;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using AgonesSdkCsharp;
using AgonesSdkCsharp.Hosting;

// reference implementation: https://github.com/googleforgames/agones/blob/a972b6be311b062e2dfaaa0ba5ebbe44109a25e9/examples/simple-udp/main.go
namespace Agones
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await BatchHost.CreateDefaultBuilder()
                .UseAgones<AgonesSdk>()
                .ConfigureLogging((hostContext, logging) => logging.SetMinimumLevel(LogLevel.Debug))
                .RunBatchEngineAsync<EchoUdpServerBatch>(args);
        }
    }

    public class EchoUdpServerBatch : BatchBase
    {
        readonly IAgonesSdk _agonesSdk;
        readonly string host = "0.0.0.0";
        readonly int port = 7654;

        readonly ILogger logger;

        public EchoUdpServerBatch(ILoggerFactory loggerFactory, IAgonesSdk agonesSdk)
        {
            this.logger = loggerFactory.CreateLogger<EchoUdpServer>();
            _agonesSdk = agonesSdk;
        }

        [Command("run", "run echo server")]
        public async Task RunEchoServer()
        {
            logger.LogInformation($"{DateTime.Now} Starting Echo UdpServer with AgonesSdk. {host}:{port}");
            await new EchoUdpServer(host, port, _agonesSdk, logger, Context.CancellationToken).ServerLoop();
        }
    }

    public static class TaskExtensions
    {
        public static void FireAndForget(this Task task, Action<Task> action)
        {
            task.ContinueWith(x =>
            {
                action(x);
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
