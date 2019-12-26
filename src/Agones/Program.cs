using System;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using MicroBatchFramework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Microsoft.Extensions.Hosting;

// reference implementation: https://github.com/googleforgames/agones/blob/a972b6be311b062e2dfaaa0ba5ebbe44109a25e9/examples/simple-udp/main.go
namespace Agones
{
    class Program
    {
        private static readonly Lazy<Random> jitterer = new Lazy<Random>(() => new Random());
        internal static readonly string ClientName = "Agones";

        static async Task Main(string[] args)
        {
            await BatchHost.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // note: if want to handle httpclient log entirely: https://www.stevejgordon.co.uk/httpclientfactory-asp-net-core-logging
                    // retry failed for max 3 times with exponential back-off + zitter
                    // also circuitbreak for 5 times failed, then 30sec block.
                    services.AddHttpClient(ClientName, client => client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json")))
                        .AddTransientHttpErrorPolicy(x => x.WaitAndRetryAsync(3, retry => ExponentialBackkoff(retry)))
                        .AddTransientHttpErrorPolicy(x => x.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));
                    services.AddSingleton<IAgonesSdk, AgonesSdk>();
                    services.AddHostedService<AgonesHostedService>();
                })
                .ConfigureLogging(logging => logging.AddFilter($"System.Net.Http.HttpClient.{ClientName}", LogLevel.Warning))
                .RunBatchEngineAsync<EchoUdpServerBatch>(args);
        }

        static TimeSpan ExponentialBackkoff(int retryAttempt)
            => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + TimeSpan.FromMilliseconds(jitterer.Value.Next(0, 100));
    }

    public class EchoUdpServerBatch : BatchBase
    {
        readonly IAgonesSdk _agonesSdk;
        readonly string host = "0.0.0.0";
        readonly int port = 7654;

        readonly ILogger<EchoUdpServerBatch> logger;

        public EchoUdpServerBatch(ILogger<EchoUdpServerBatch> logger, IAgonesSdk agonesSdk)
        {
            this.logger = logger;
            _agonesSdk = agonesSdk;
        }

        [Command("run", "run echo server")]
        public async Task RunEchoServer()
        {
            logger.LogInformation($"{DateTime.Now} Starting Echo UdpServer with AgonesSdk. {host}:{port}");
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
