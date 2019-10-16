using KeySmith.Internals.Scripts;
using KeySmith.Internals.Scripts.Parameters;
using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace KeySmith
{
    class MemoLockService : IMemoLockService
    {
        private readonly IMemoScriptLibrary _scriptLibrary;
        private readonly ILockService _lockService;

        public MemoLockService(IMemoScriptLibrary scriptLibrary, ILockService lockService)
        {
            _scriptLibrary = scriptLibrary ?? throw new ArgumentNullException(nameof(scriptLibrary));
            _lockService = lockService ?? throw new ArgumentNullException(nameof(lockService));
        }

        public async Task<RedisValue> MemoLockAsync(MemoKey key, Func<CancellationToken, Task<RedisValue>> generator, CancellationToken cancellationToken)
        {
            var taskSource = new TaskCompletionSource<RedisValue>();
            using (var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var handler = GetHandler(source, taskSource);
                try
                {
                    await _scriptLibrary.SubscribeAsync(key.GetSubscribtionChannel(), handler).ConfigureAwait(false);

                    var anyTask = Task.WhenAny(taskSource.Task, MemoLockWithoutSubscriptionAsync(key, generator, source.Token));
                    var first = await anyTask.ConfigureAwait(false);
                    if (first.IsFaulted)
                    {
                        throw first.Exception.InnerException;
                    }
                    return first.Result;
                }
                finally
                {
                    await _scriptLibrary.UnsubscribeAsync(key.GetSubscribtionChannel(), handler).ConfigureAwait(false);
                }
            }
        }

        private async Task<RedisValue?> TryGetCachedValue(MemoKey key)
        {
            var result = await _scriptLibrary.GetValuesAsync(key.GetValueKey(), key.GetErrorKey()).ConfigureAwait(false);
            if (result[0] != RedisValue.Null)
            {
                return result[0];
            }
            if (result[1] != RedisValue.Null)
            {
                throw new GenerationException(result[1]);
            }
            return null;
        }

        private async Task<RedisValue> MemoLockWithoutSubscriptionAsync(MemoKey key, Func<CancellationToken, Task<RedisValue>> generator, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new TaskCanceledException();
            }

            var result = await TryGetCachedValue(key).ConfigureAwait(false);
            if (result != null)
            {
                return result.Value;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                throw new TaskCanceledException();
            }

            try
            {
                return await _lockService.LockAsync(key.GetLockKey(), async c =>
                {
                    //in case we missed the publication
                    var result = await TryGetCachedValue(key).ConfigureAwait(false);
                    if (result != null)
                    {
                        return result.Value;
                    }
                    return await LockedCallback(key, generator, c).ConfigureAwait(false);
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                result = await TryGetCachedValue(key).ConfigureAwait(false);
                if (result != null)
                {
                    return result.Value;
                }
                throw;
            }
        }

        private async Task<RedisValue> LockedCallback(MemoKey key, Func<CancellationToken, Task<RedisValue>> generator, CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                throw new TaskCanceledException();
            }
            try
            {
                var result = await generator(token).ConfigureAwait(false);

                await _scriptLibrary.PublishAsync(new MemoSetValueParameters(key, result)).ConfigureAwait(false);

                return result;
            }
            catch (Exception e)
            {
                await _scriptLibrary.PublishAsync(new MemoSetValueParameters(key, e)).ConfigureAwait(false);
                throw;
            }
        }

        private Action<RedisChannel, RedisValue> GetHandler(CancellationTokenSource source, TaskCompletionSource<RedisValue> task) => (c, v) =>
        {
            if (c.ToString().Contains("/memoerrornotif:"))
            {
                task.TrySetException(new GenerationException(v));
            }
            else
            {
                task.TrySetResult(v);
            }

            try
            {
                source.Cancel();
            }
            catch (ObjectDisposedException)
            {

            }
        };
    }
}