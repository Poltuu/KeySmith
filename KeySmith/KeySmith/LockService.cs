using KeySmith.Internals.Locks;
using KeySmith.Internals.Scripts;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace KeySmith
{
    internal class LockService : ILockService
    {
        private readonly IScriptLibrary _scriptLibrary;

        public LockService(IScriptLibrary scriptLibrary)
        {
            _scriptLibrary = scriptLibrary ?? throw new ArgumentNullException(nameof(scriptLibrary));
        }

        public Task LockAsync(Key key, Func<CancellationToken, Task> callback, CancellationToken cancellationToken)
            => LockAsync(key, async c => { await callback(c).ConfigureAwait(true); return true; }, cancellationToken);

        public async Task<T> LockAsync<T>(Key key, Func<CancellationToken, Task<T>> callback, CancellationToken cancellationToken)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            using var state = new LockState(key, cancellationToken);
            try
            {
                await _scriptLibrary.SubscribeAsync(state).ConfigureAwait(false);

                try
                {
                    if (await _scriptLibrary.GetLockOrAddToQueue(state.Parameters).ConfigureAwait(false))
                    {
                        state.SetWithKey();
                    }

                    //we start protecting after the first manipulation in redis, to properly hit the expiration if needed
                    using (var protector = new LockProtector(_scriptLibrary, state))
                    {
                        await state.WaitingTask.ConfigureAwait(false);
                        return await callback(state.Token).ConfigureAwait(false);
                    }
                }
                finally
                {
                    await _scriptLibrary.FreeLockAndPop(state.Parameters).ConfigureAwait(false);
                }
            }
            finally
            {
                await _scriptLibrary.UnSubscribeAsync(state).ConfigureAwait(false);
            }
        }
    }
}