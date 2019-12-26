using System.Threading;
using System.Threading.Tasks;

namespace Agones
{
    // should implement: ready,allocate,setlabel,setannotation,gameserver,health,shutdown,watch
    // https://agones.dev/site/docs/guides/client-sdks/#rest-api-implementation
    // spec: https://github.com/googleforgames/agones/blob/release-1.0.0/sdk.swagger.json
    // ref: sdk server https://github.com/googleforgames/agones/blob/deab3ce0e521a98231a0ca00834276431980e7e1/pkg/sdk/sdk.pb.go#L546
    public interface IAgonesSdk
    {
        bool HealthEnabled { get; set; }

        Task StartAsync(CancellationToken token);
        Task StopAsync();

        /// <summary>
        /// Call when the GameServer is ready
        /// </summary>
        /// <remarks>/Ready</remarks>
        /// <returns></returns>
        Task<bool> Ready();
        /// <summary>
        /// Call to self Allocation the GameServer (POST)
        /// </summary>
        /// <remarks>/Allocate</remarks>
        /// <returns></returns>
        Task<bool> Allocate();
        /// <summary>
        /// Call when the GameServer is shutting down
        /// </summary>
        /// <remarks>/Shutdown</remarks>
        /// <returns></returns>
        Task<bool> Shutdown();
        /// <summary>
        /// Send a Empty every d Duration to declare that this GameSever is healthy
        /// </summary>
        /// <remarks>/Health (stream)</remarks>
        /// <returns></returns>
        Task<bool> Health();
        /// <summary>
        /// Retrieve the current GameServer data
        /// </summary>
        /// <remarks>/GetGameServer</remarks>
        /// <returns></returns>
        Task<(bool ok, GameServerResponse response)> GameServer();
        /// <summary>
        /// Send GameServer details whenever the GameServer is updated
        /// </summary>
        /// <remarks>/WatchGameServer (stream)</remarks>
        /// <returns></returns>
        Task<(bool, GameServerResponse)> Watch();
        /// <summary>
        /// Apply a Label to the backing GameServer metadata
        /// </summary>
        /// <remarks>/SetLabel</remarks>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        Task<bool> SetLabel(string key, string value);
        /// <summary>
        /// Apply a Annotation to the backing GameServer metadata
        /// </summary>
        /// <remarks>/SetAnnotation</remarks>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        Task<bool> SetAnnotation(string key, string value);
        /// <summary>
        /// Marks the GameServer as the Reserved state for Duration
        /// </summary>
        /// <remarks>/Reserve</remarks>
        /// <returns></returns>
        Task<bool> Reserve(int seconds);
    }
}