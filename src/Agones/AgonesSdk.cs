using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Agones
{
    // ref: sdk sample https://github.com/googleforgames/agones/blob/release-1.0.0/sdks/go/sdk.go
    public class AgonesSdk : IAgonesSdk
    {
        public int HealthIntervalSecond { get; set; } = 2;
        public bool HealthEnabled { get; set; } = true;
        static readonly Encoding encoding = new UTF8Encoding(false);
        static readonly ConcurrentDictionary<string, StringContent> jsonCache = new ConcurrentDictionary<string, StringContent>();

        // ref: sdk server https://github.com/googleforgames/agones/blob/master/cmd/sdk-server/main.go
        // grpc: localhost on port 59357
        // http: localhost on port 59358
        readonly Uri SideCarAddress = new Uri("http://127.0.0.1:59358");
        readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        readonly IHttpClientFactory _httpClientFactory;
        readonly ILogger<IAgonesSdk> _logger;

        public AgonesSdk(IHttpClientFactory httpClientFactory, ILogger<IAgonesSdk> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            jsonCache.TryAdd("{}", new StringContent("{}", encoding, "application/json"));
        }

        // entrypoint for IHostedService
        public Task StartAsync(CancellationToken token)
        {
            if (token == null)
            {
                token = cancellationTokenSource.Token;
            }
            var task = HealthCheckAsync(token);
            return task;
        }

        // exit for IHostedService
        public Task StopAsync()
        {
            cancellationTokenSource?.Dispose();
            return Task.CompletedTask;
        }

        public async Task<bool> Ready()
        {
            _logger.LogInformation("Calling Ready sdk.");
            var (ok, _) = await SendRequestAsync<NullResponse>("/ready", "{}");
            return ok;
        }

        public async Task<bool> Allocate()
        {
            _logger.LogInformation("Calling Allocate sdk.");
            var (ok, _) = await SendRequestAsync<NullResponse>("/allocate", "{}");
            return ok;
        }

        public async Task<bool> Shutdown()
        {
            _logger.LogInformation("Calling Shutdown sdk.");
            var (ok, _) = await SendRequestAsync<NullResponse>("/shutdown", "{}");
            return ok;
        }

        public async Task<bool> Health()
        {
            _logger.LogInformation($"{DateTime.Now} Calling Health sdk.");
            var (ok, _) = await SendRequestAsync<NullResponse>("/health", "{}");
            return ok;
        }

        public async Task<(bool, GameServerResponse)> GameServer()
        {
            // TODO: return GameServer
            _logger.LogInformation("Calling GetGameServer sdk.");
            var response = await SendRequestAsync<GameServerResponse>("/gameserver", "{}", HttpMethod.Get);
            return response;
        }

        public async Task<(bool, GameServerResponse)> Watch()
        {
            _logger.LogInformation("Calling WatchGameServer sdk.");
            var response = await SendRequestAsync<GameServerResponse>("/watch/gameserver", "{}", HttpMethod.Get);
            return response;
        }

        public async Task<bool> Reserve(int seconds)
        {
            _logger.LogInformation("Calling Reserve sdk.");
            string json = Utf8Json.JsonSerializer.ToJsonString(new ReserveBody(seconds));
            var (ok, _) = await SendRequestAsync<NullResponse>("/reserve", json);
            return ok;
        }

        public async Task<bool> SetLabel(string key, string value)
        {
            _logger.LogInformation("Calling SetLabel sdk.");
            string json = Utf8Json.JsonSerializer.ToJsonString(new KeyValueMessage(key, value));
            var (ok, _) = await SendRequestAsync<NullResponse>("/metadata/label", json, HttpMethod.Put);
            return ok;
        }

        public async Task<bool> SetAnnotation(string key, string value)
        {
            _logger.LogInformation("Calling SetAnnotation sdk.");
            string json = Utf8Json.JsonSerializer.ToJsonString(new KeyValueMessage(key, value));
            var (ok, _) = await SendRequestAsync<NullResponse>("/metadata/annotation", json, HttpMethod.Put);
            return ok;
        }

        public async Task HealthCheckAsync(CancellationToken ct)
        {
            while (HealthEnabled)
            {
                if (ct.IsCancellationRequested) throw new OperationCanceledException();

                try
                {
                    await Health();
                }
                catch (ObjectDisposedException oex)
                {
                    _logger.LogError($"health detect error, let retry. {oex.Message}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"health detect error, let retry. {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromSeconds(HealthIntervalSecond));
            }
        }

        private async Task<(bool, TResponse)> SendRequestAsync<TResponse>(string api, string json, bool useCache = true) where TResponse : class
        {
            return await SendRequestAsync<TResponse>(api, json, HttpMethod.Post, useCache);
        }
        private async Task<(bool, TResponse)> SendRequestAsync<TResponse>(string api, string json, HttpMethod method, bool useCache = true) where TResponse : class
        {
            TResponse response = null;
            if (cancellationTokenSource.IsCancellationRequested) return (false, response);

            var httpClient = _httpClientFactory.CreateClient("agones");
            httpClient.BaseAddress = SideCarAddress;
            var requestMessage = new HttpRequestMessage(method, api);
            try
            {
                if (useCache)
                {
                    if (jsonCache.TryGetValue(json, out var cachedContent))
                    {
                        requestMessage.Content = cachedContent;
                    }
                    else
                    {
                        var stringContent = new StringContent(json, encoding, "application/json");
                        stringContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                        jsonCache.TryAdd(json, stringContent);
                    }
                }
                var res = await httpClient.SendAsync(requestMessage);
                _logger.LogInformation($"Agones SendRequest ok: {api} {response}");

                // result
                var content = await res.Content.ReadAsByteArrayAsync();
                if (content != null)
                {
                    response = JsonSerializer.Deserialize<TResponse>(content);
                }
                var isOk = res.StatusCode == HttpStatusCode.OK;
                return (isOk, response);
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"Agones SendRequest failed: {api} {ex.GetType().FullName} {ex.Message} {ex.StackTrace}");
                return (false, response);
            }
        }

        public class ReserveBody
        {
            public int Seconds { get; set; }
            public ReserveBody(int seconds) => Seconds = seconds;
        }

        public class KeyValueMessage
        {
            public string Key;
            public string Value;
            public KeyValueMessage(string key, string value) => (Key, Value) = (key, value);
        }
    }
}
