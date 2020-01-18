using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AgonesSdkCsharp;
using MicroBatchFramework;
using Microsoft.Extensions.Logging;

namespace Agones
{
    public class EchoUdpServer
    {
        private readonly string _ipAddress;
        private readonly int _port;
        private readonly Encoding _encoding;
        private readonly IAgonesSdk _agonesSdk;
        private readonly ILogger _logger;
        private readonly CancellationToken _ct;

        public EchoUdpServer(string ipAddress, int port, IAgonesSdk agnoesSdk, ILogger logger, CancellationToken ct)
        {
            _ipAddress = ipAddress;
            _port = port;
            _encoding = new UTF8Encoding(false);
            _agonesSdk = agnoesSdk;
            _logger = logger;
            _ct = ct;
        }

        public async Task ServerLoop()
        {
            //_logger.LogInformation($"{DateTime.Now} Starting Health Ping");
            //_agonesSdk.StartAsync(null).FireAndForget(x => _logger.LogError($"TaskUnhandled: {x.Exception}"));

            var done = false;
            var crashed = false;

            Console.WriteLine($"Starting UDP server, listening on port {_port}");
            var listener = new IPEndPoint(IPAddress.Parse(_ipAddress), _port);
            using (var udpClient = new UdpClient(listener))
            {
                Console.WriteLine("Marking this server as ready");
                await _agonesSdk.Ready(_ct);

                while (!done)
                {
                    try
                    {
                        await WaitUdpClient(udpClient, done, crashed);
                    }
                    catch (OperationCanceledException)
                    {
                        udpClient.Close();
                        throw;
                    }
                }
            }
            if (done) Environment.Exit(0);
            if (crashed) Environment.Exit(1);
        }

        private async Task WaitUdpClient(UdpClient udpClient, bool done, bool crashed)
        {
            var receive = await udpClient.ReceiveAsync().WithCancellation(_ct);
            var (sender, txt) = (receive.RemoteEndPoint, _encoding.GetString(receive.Buffer)?.TrimStart()?.TrimEnd());
            var parts = txt.Split(' ');
            switch (parts[0])
            {
                case "EXIT":
                    _logger.LogInformation("Shutdown gameserver.");
                    done = true;
                    await _agonesSdk.Shutdown(_ct);
                    var exitMessage = _encoding.GetBytes("ACK: " + txt + "\n");
                    await udpClient.SendAsync(exitMessage, exitMessage.Length, sender);
                    break;
                case "UNHEALTHY":
                    _logger.LogInformation("Turns off health pings.");
                    _agonesSdk.HealthEnabled = false;
                    break;
                case "GAMESERVER":
                    var response = await _agonesSdk.GameServer(_ct);
                    var gameserverMessage = _encoding.GetBytes(response.Status.Address + ":" + response.Status.Ports[0].Port + "\n");
                    await udpClient.SendAsync(gameserverMessage, gameserverMessage.Length, sender);
                    break;
                case "READY":
                    await _agonesSdk.Ready(_ct);
                    break;
                case "ALLOCATE":
                    await _agonesSdk.Allocate(_ct);
                    break;
                case "RESERVE":
                    int.TryParse(parts[1], out var seconds);
                    await _agonesSdk.Reserve(seconds, _ct);
                    break;
                case "WATCH":
                    await _agonesSdk.Watch(_ct);
                    break;
                case "LABEL":
                    switch (parts.Length)
                    {
                        case 1:
                            // legacy format
                            await _agonesSdk.Label("timestamp", DateTime.Now.ToUniversalTime().ToString(), _ct);
                            break;
                        case 3:
                            await _agonesSdk.Label(parts[1], parts[2], _ct);
                            break;
                        default:
                            var labelMessage = _encoding.GetBytes("ERROR: Invalid LABEL command, must use zero or 2 arguments\n");
                            await udpClient.SendAsync(labelMessage, labelMessage.Length, sender);
                            return;
                    }
                    break;
                case "ANNOTATION":
                    switch (parts.Length)
                    {
                        case 1:
                            // legacy format
                            await _agonesSdk.Annotation("timestamp", DateTime.UtcNow.ToUniversalTime().ToString(), _ct);
                            break;
                        case 3:
                            await _agonesSdk.Annotation(parts[1], parts[2], _ct);
                            break;
                        default:
                            var labelMessage = _encoding.GetBytes("ERROR: Invalid ANNOTATION command, must use zero or 2 arguments\n");
                            await udpClient.SendAsync(labelMessage, labelMessage.Length, sender);
                            return;
                    }
                    break;
                case "CRASH":
                    _logger.LogInformation("Crashing.");
                    done = true;
                    crashed = true;
                    throw new Exception("Force crash by Client request.");
                default:
                    var echoMessage = _encoding.GetBytes("ACK: " + txt + "\n");
                    await udpClient.SendAsync(echoMessage, echoMessage.Length, sender);
                    break;
            }

        }
    }
}
