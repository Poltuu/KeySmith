﻿using KeySmith.Internals.Scripts.Parameters;
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

        private readonly object _stateLocker = new();

        public LockState(Key key, string identifier, CancellationToken cancellationToken)
        {
            Key = key;
            Identifier = identifier;
            State = State.WaitingForKey;

            QueueInRedis = new TaskCompletionSource<bool>(key);
            CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            Parameters = new LockLuaParameters(identifier, key);
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

        public void Handler(RedisChannel _, RedisValue key)
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