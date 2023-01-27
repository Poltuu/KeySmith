using KeySmith.Internals.Scripts;
using System;
using System.Threading.Tasks;

namespace KeySmith.Internals.Locks
{
    readonly struct LockProtector : IDisposable
    {
        private readonly IScriptLibrary _scriptLibrary;
        private readonly LockState _state;

        public LockProtector(IScriptLibrary scriptLibrary, LockState state)
        {
            _scriptLibrary = scriptLibrary;
            _state = state;

            EnsureNoDeadLock();
        }

        /// <summary>
        /// for testing purposes only
        /// </summary>
        /// <param name="scriptLibrary"></param>
        /// <param name="state"></param>
        /// <param name="startprotection"></param>
        internal LockProtector(IScriptLibrary scriptLibrary, LockState state, bool startprotection)
        {
            _scriptLibrary = scriptLibrary;
            _state = state;

            if (startprotection)
            {
                EnsureNoDeadLock();
            }
        }

        internal async void EnsureNoDeadLock() => await EnsureNoDeadLockAsync().ConfigureAwait(false);

        //for testing purposes
        internal async Task EnsureNoDeadLockAsync()
        {
            if (_state.State == State.Done)
            {
                return;
            }

            //this will be repeated until disposed
            while (true)
            {
                try
                {
                    //we wait for the time it takes the key to expire
                    await Task.Delay(_state.Key.RedisKeyExpiration, _state.Token).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    //if the token gets canceled everything went well
                    return;
                }

                if (_state.State == State.Done)
                {
                    return;
                }

                try
                {
                    //let's see what's in redis at this point
                    var result = await _scriptLibrary.GetKeySituation(_state.Parameters).ConfigureAwait(false);
                    switch (result)
                    {
                        //lock in waiting list
                        case 0:
                            //we keep waiting
                            break;

                        //we own the lock
                        case 1:
                            //this means the task has been completed while we were looking
                            //we set the result and keep protecting the callback
                            _state.SetWithKey();
                            break;

                        //lock is not found
                        case 2:
                            switch (_state.State)
                            {
                                case State.WaitingForKey:
                                    //keys have expired, probably due to remote failure, we need to restart the lock process
                                    if (await _scriptLibrary.GetLockOrAddToQueue(_state.Parameters).ConfigureAwait(false))
                                    {
                                        _state.SetWithKey();
                                    }
                                    break;

                                case State.WithKey:
                                    //we thought we had the key but didn't
                                    //we stop everything
                                    _state.SetDone();
                                    return;//no need to wait for delay cancellation

                                case State.Done: return;
                            }
                            break;

                        default:
                            _state.SetDone(new NotImplementedException("Unexpected Redis state"));
                            return;
                    }
                }
                catch (Exception e)
                {
                    //Redis failed, we need to stop waiting for the notification
                    _state.SetDone(e);
                    return;
                }
            }
        }

        public void Dispose() => _state.SetDone();
    }
}