using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agones
{
    public class AgonesHostedService : IHostedService
    {
        readonly IAgonesSdk _agonesSdk;
        readonly ILogger<AgonesHostedService> _logger;
        public AgonesHostedService(IAgonesSdk agonesSdk, ILogger<AgonesHostedService> logger)
        {
            _agonesSdk = agonesSdk;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"{DateTime.Now} Starting Health Ping");
            await _agonesSdk.StartAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _agonesSdk.StopAsync();
        }
    }
}