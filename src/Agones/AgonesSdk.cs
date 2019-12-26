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
    // ref: sdk sample https://github.com/googleforgames/agones/blob/release-1.2.0/sdks/go/sdk.go
    public class AgonesSdk : IAgonesSdk
    {
        public int HealthIntervalSecond { get; set; } = 2;
        public bool HealthEnabled { get; set; } = true;
        static readonly Encoding encoding = new UTF8Encoding(false);
        static readonly ConcurrentDictionary<string, StringContent> jsonCache = new ConcurrentDictionary<string, StringContent>();

        // ref: sdk server https://github.com/googleforgames/agones/blob/master/cmd/sdk-server/main.go
        // grpc: localhost on port 9357
        // http: localhost on port 9358
        readonly Uri SideCarAddress = new Uri("http://127.0.0.1:9358");
        readonly CancellationTokenSource cts = new CancellationTokenSource();
        readonly IHttpClientFactory _httpClientFactory;
        readonly ILogger<IAgonesSdk> _logger;

        static int healthCount = 0;

        public AgonesSdk(IHttpClientFactory httpClientFactory, ILogger<IAgonesSdk> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            // cache empty request content
            var stringContent = new StringContent("{}", encoding, "application/json");
            stringContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            jsonCache.TryAdd("{}", stringContent);
        }

        // entrypoint for IHostedService
        public Task StartAsync(CancellationToken token)
        {
            if (token == null)
            {
                token = cts.Token;
            }
            var task = HealthCheckAsync(token);
            return task;
        }

        // exit for IHostedService
        public Task StopAsync()
        {
            cts?.Dispose();
            return Task.CompletedTask;
        }

        public async Task<bool> Ready()
        {
            _logger.LogDebug($"{DateTime.Now} {nameof(AgonesSdk)} Calling sdk {nameof(Ready)}.");
            var (ok, _) = await SendRequestAsync<NullResponse>("/ready", "{}");
            return ok;
        }

        public async Task<bool> Allocate()
        {
            _logger.LogDebug($"{DateTime.Now} {nameof(AgonesSdk)} Calling sdk {nameof(Allocate)}.");
            var (ok, _) = await SendRequestAsync<NullResponse>("/allocate", "{}");
            return ok;
        }

        public async Task<bool> Shutdown()
        {
            _logger.LogDebug($"{DateTime.Now} {nameof(AgonesSdk)} Calling sdk {nameof(Shutdown)}.");
            var (ok, _) = await SendRequestAsync<NullResponse>("/shutdown", "{}");
            return ok;
        }

        public async Task<bool> Health()
        {
            if ((healthCount % 10) == 0)
                _logger.LogInformation($"{DateTime.Now} {nameof(AgonesSdk)} health called for {healthCount}.");
            _logger.LogDebug($"{DateTime.Now} {nameof(AgonesSdk)} Calling sdk {nameof(Health)}.");
            var (ok, _) = await SendRequestAsync<NullResponse>("/health", "{}");
            healthCount++;
            return ok;
        }

        public async Task<(bool, GameServerResponse)> GameServer()
        {
            // TODO: return GameServer
            _logger.LogDebug($"{DateTime.Now} {nameof(AgonesSdk)} Calling sdk {nameof(GameServer)}.");
            var response = await SendRequestAsync<GameServerResponse>("/gameserver", "{}", HttpMethod.Get);
            return response;
        }

        public async Task<(bool, GameServerResponse)> Watch()
        {
            _logger.LogDebug($"{DateTime.Now} {nameof(AgonesSdk)} Calling sdk {nameof(Watch)}.");
            var response = await SendRequestAsync<GameServerResponse>("/watch/gameserver", "{}", HttpMethod.Get);
            return response;
        }

        public async Task<bool> Reserve(int seconds)
        {
            _logger.LogDebug($"{DateTime.Now} {nameof(AgonesSdk)} Calling sdk {nameof(Reserve)}.");
            string json = Utf8Json.JsonSerializer.ToJsonString(new ReserveBody(seconds));
            var (ok, _) = await SendRequestAsync<NullResponse>("/reserve", json);
            return ok;
        }

        public async Task<bool> Label(string key, string value)
        {
            _logger.LogDebug($"{DateTime.Now} {nameof(AgonesSdk)} Calling sdk {nameof(Label)}.");
            string json = Utf8Json.JsonSerializer.ToJsonString(new KeyValueMessage(key, value));
            var (ok, _) = await SendRequestAsync<NullResponse>("/metadata/label", json, HttpMethod.Put);
            return ok;
        }

        public async Task<bool> Annotation(string key, string value)
        {
            _logger.LogDebug($"{DateTime.Now} {nameof(AgonesSdk)} Calling sdk {nameof(Annotation)}.");
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

                await Task.Delay(TimeSpan.FromSeconds(HealthIntervalSecond), cts.Token);
            }
        }

        private async Task<(bool, TResponse)> SendRequestAsync<TResponse>(string api, string json, bool useCache = true) where TResponse : class
        {
            return await SendRequestAsync<TResponse>(api, json, HttpMethod.Post, useCache);
        }
        private async Task<(bool, TResponse)> SendRequestAsync<TResponse>(string api, string json, HttpMethod method, bool useCache = true) where TResponse : class
        {
            TResponse response = null;
            if (cts.IsCancellationRequested) throw new OperationCanceledException(cts.Token);

            var httpClient = _httpClientFactory.CreateClient(Program.ClientName);
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
                _logger.LogDebug($"Agones SendRequest ok: {api} {response}");

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
                _logger.LogDebug($"Agones SendRequest failed: {api} {ex.GetType().FullName} {ex.Message} {ex.StackTrace}");
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
