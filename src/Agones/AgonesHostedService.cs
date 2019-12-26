using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agones
{
    public class AgonesHostedService : BackgroundService
    {
        readonly IAgonesSdk _agonesSdk;
        readonly ILogger<AgonesHostedService> _logger;
        public AgonesHostedService(IAgonesSdk agonesSdk, ILogger<AgonesHostedService> logger)
        {
            _agonesSdk = agonesSdk;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"{DateTime.Now} {nameof(AgonesHostedService)} Starting Health Ping");
            await _agonesSdk.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"{DateTime.Now} {nameof(AgonesHostedService)} Cancel requested");
            await _agonesSdk.StopAsync();
        }
    }
}