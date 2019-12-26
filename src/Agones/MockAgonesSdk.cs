using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Agones
{
    class MockAgonesSdk : IHostedService, IAgonesSdk
    {
        public bool HealthEnabled { get; set; } = true;

        public Task StartAsync()
        {
            return Task.CompletedTask;
        }
        public Task StopAsync()
        {
            return Task.CompletedTask;
        }

        public Task<bool> Allocate()
        {
            return Task.FromResult<bool>(true);
        }

        public Task<(bool, GameServerResponse)> GameServer()
        {
            return Task.FromResult<(bool, GameServerResponse)>((true, null));
        }
        public Task<(bool, GameServerResponse)> Watch()
        {
            // stream どうするの?
            return Task.FromResult<(bool, GameServerResponse)>((true, null));
        }

        public Task<bool> Health()
        {
            return Task.FromResult<bool>(true);
        }

        public Task<bool> Ready()
        {
            return Task.FromResult<bool>(true);
        }

        public Task<bool> Reserve(int seconds)
        {
            return Task.FromResult<bool>(true);
        }

        public Task<bool> SetAnnotation(string key, string value)
        {
            return Task.FromResult<bool>(true);
        }

        public Task<bool> SetLabel(string key, string value)
        {
            return Task.FromResult<bool>(true);
        }

        public Task<bool> Shutdown()
        {
            return Task.FromResult<bool>(true);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
