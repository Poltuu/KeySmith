using KeySmith.Internals.Locks;
using KeySmith.Internals.Scripts.Parameters;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("KeySmith.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
namespace KeySmith.Internals.Scripts
{
    internal interface IScriptLibrary
    {
        Task<bool> GetLockOrAddToQueue(LockLuaParameters parameters);

        Task FreeLockAndPop(LockLuaParameters parameters);

        Task<int> GetKeySituation(LockLuaParameters parameters);

        Task SubscribeAsync(LockState state);
        Task UnSubscribeAsync(LockState state);
    }
}