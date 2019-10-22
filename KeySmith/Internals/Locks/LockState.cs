using KeySmith.Internals.Scripts.Parameters;
using StackExchange.Redis;
using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace KeySmith.Internals.Locks
{
    class LockState : IDisposable
    {
        public State State { get; private set; }
        public Key Key { get; private set; }
        public string Identifier { get; private set; }

        private readonly TaskCompletionSource<bool> QueueInRedis;
        public Task WaitingTask => QueueInRedis.Task;

        private readonly CancellationTokenSource CancellationTokenSource;
        public CancellationToken Token => CancellationTokenSource.Token;

        public LockLuaParameters Parameters { get; }

        private readonly object _stateLocker = new object();

        public LockState(Key key, string identifier, CancellationToken cancellationToken)
        {
            Key = key;
            Identifier = identifier;
            State = State.WaitingForKey;

            QueueInRedis = new TaskCompletionSource<bool>(key);
            CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            Parameters = new LockLuaParameters(identifier, key);
        }

        private static readonly char[] _chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890-_".ToCharArray();
        private static string GetUniqueKey(int size)
        {
            var data = new byte[size];
            using (var crypto = new RNGCryptoServiceProvider())
            {
                crypto.GetBytes(data);
            }
            var result = new System.Text.StringBuilder(size);
            foreach (var b in data)
            {
                result.Append(_chars[b % _chars.Length]);
            }
            return result.ToString();
        }

        public void SetWithKey()
        {
            lock (_stateLocker)
            {
                switch (State)
                {
                    case State.WaitingForKey:
                        QueueInRedis.TrySetResult(true);
                        break;
                    case State.Done:
                        throw new Exception("Invalid state transition from 'Done' to 'WithKey'.");
                }
                State = State.WithKey;
            }
        }

        public void SetDone(Exception? exception = null)
        {
            lock (_stateLocker)
            {
                try
                {
                    CancellationTokenSource.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }

                if (exception == null)
                {
                    QueueInRedis.TrySetCanceled(Token);
                }
                else
                {
                    QueueInRedis.TrySetException(exception);
                }
                State = State.Done;
            }
        }

        public void Handler(RedisChannel channel, RedisValue key)
        {
            switch (State)
            {
                case State.WaitingForKey:
                    if (key == Identifier)
                    {
                        SetWithKey();
                    }
                    break;

                case State.WithKey:
                    if (key != Identifier)
                    {
                        SetDone();
                    }
                    break;
            }
        }

        public void Dispose() => CancellationTokenSource.Dispose();
    }
}