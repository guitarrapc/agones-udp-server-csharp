using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
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
        private readonly ILogger<BatchEngine> _logger;

        public EchoUdpServer(string ipAddress, int port, IAgonesSdk agnoesSdk, ILogger<BatchEngine> logger)
        {
            _ipAddress = ipAddress;
            _port = port;
            _encoding = new UTF8Encoding(false);
            _agonesSdk = agnoesSdk;
            _logger = logger;
        }

        public async Task ServerLoop()
        {
            //_logger.LogInformation($"{DateTime.Now} Starting Health Ping");
            //_agonesSdk.StartAsync(null).FireAndForget(x => _logger.LogError($"TaskUnhandled: {x.Exception}"));

            var done = false;
            var exited = false;
            var crashed = false;

            Console.WriteLine($"Starting UDP server, listening on port {_port}");
            var listener = new IPEndPoint(IPAddress.Parse(_ipAddress), _port);
            using (var udpClient = new UdpClient(listener))
            {
                Console.WriteLine("Marking this server as ready");
                var isReady = await _agonesSdk.Ready();
                if (!isReady) throw new Exception("Could not prepare Agones.");

                while (!done)
                {
                    var receive = await udpClient.ReceiveAsync();
                    var (sender, txt) = (receive.RemoteEndPoint, _encoding.GetString(receive.Buffer)?.TrimStart()?.TrimEnd());
                    var parts = txt.Split(' ');
                    switch (parts[0])
                    {
                        case "EXIT":
                            _logger.LogInformation("Shutdown gameserver.");
                            done = true;
                            await _agonesSdk.Shutdown();
                            var exitMessage = _encoding.GetBytes("ACK: " + txt + "\n");
                            await udpClient.SendAsync(exitMessage, exitMessage.Length, sender);
                            break;
                        case "UNHEALTHY":
                            _logger.LogInformation("Turns off health pings.");
                            _agonesSdk.HealthEnabled = false;
                            break;
                        case "GAMESERVER":
                            var gameserver = await _agonesSdk.GameServer();
                            var gameserverMessage = _encoding.GetBytes(gameserver.response.status.address + ":" + gameserver.response.status.ports[0].port + "\n");
                            await udpClient.SendAsync(gameserverMessage, gameserverMessage.Length, sender);
                            break;
                        case "READY":
                            await _agonesSdk.Ready();
                            break;
                        case "ALLOCATE":
                            await _agonesSdk.Allocate();
                            break;
                        case "RESERVE":
                            int.TryParse(parts[1], out var seconds);
                            await _agonesSdk.Reserve(seconds);
                            break;
                        case "WATCH":
                            await _agonesSdk.Watch();
                            break;
                        case "LABEL":
                            switch (parts.Length)
                            {
                                case 1:
                                    // legacy format
                                    await _agonesSdk.SetLabel("timestamp", DateTime.Now.ToUniversalTime().ToString());
                                    break;
                                case 3:
                                    await _agonesSdk.SetLabel(parts[1], parts[2]);
                                    break;
                                default:
                                    var labelMessage = _encoding.GetBytes("ERROR: Invalid LABEL command, must use zero or 2 arguments\n");
                                    await udpClient.SendAsync(labelMessage, labelMessage.Length, sender);
                                    continue;
                            }
                            break;
                        case "ANNOTATION":
                            switch (parts.Length)
                            {
                                case 1:
                                    // legacy format
                                    await _agonesSdk.SetAnnotation("timestamp", DateTime.UtcNow.ToUniversalTime().ToString());
                                    break;
                                case 3:
                                    await _agonesSdk.SetAnnotation(parts[1], parts[2]);
                                    break;
                                default:
                                    var labelMessage = _encoding.GetBytes("ERROR: Invalid ANNOTATION command, must use zero or 2 arguments\n");
                                    await udpClient.SendAsync(labelMessage, labelMessage.Length, sender);
                                    continue;
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
            if (exited) Environment.Exit(0);
            if (crashed) Environment.Exit(1);
        }
    }
}
