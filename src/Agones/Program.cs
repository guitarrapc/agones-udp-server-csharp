using System;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using MicroBatchFramework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// reference implementation: https://github.com/googleforgames/agones/blob/a972b6be311b062e2dfaaa0ba5ebbe44109a25e9/examples/simple-udp/main.go
namespace Agones
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await BatchHost.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddHttpClient("agones", client =>
                    {
                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    });
                    services.AddSingleton<IAgonesSdk, AgonesSdk>();
                    services.AddHostedService<AgonesHostedService>();
                })
                .RunBatchEngineAsync<EchoUdpServerBatch>(args);
        }
    }

    public class EchoUdpServerBatch : BatchBase
    {
        IAgonesSdk _agonesSdk;
        string host = "0.0.0.0";
        int port = 7654;

        public EchoUdpServerBatch(IAgonesSdk agonesSdk)
        {
            _agonesSdk = agonesSdk;
        }

        public async Task RunEchoServer()
        {
            Context.Logger.LogInformation($"{DateTime.Now} Starting Echo UdpServer with AgonesSdk. {host}:{port}");
            await new EchoUdpServer(host, port, _agonesSdk, Context.Logger).ServerLoop();
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
