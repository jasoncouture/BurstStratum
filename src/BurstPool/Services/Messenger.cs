using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BurstPool.Services.Interfaces;

namespace BurstPool.Services
{
    public class Messenger : IMessenger
    {
        Thread _messageQueueProcessor;
        public class Message
        {
            public Message(string @event, object sender, object data)
            {
                Key = @event;
                Sender = sender;
                Data = data;
            }

            public string Key { get; }
            public object Sender { get; }
            public object Data { get; }
        }

        public abstract class Subscription : IDisposable
        {
            public Subscription(string key)
            {
                Key = key;
            }
            public abstract void Invoke(object sender, object message);
            public string Key { get; }
            public bool Alive => !_disposed;
            private bool _disposed = false;
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
            }
        }
        public class TypedSubscription<T> : Subscription where T : class
        {
            private readonly Action<object, T> _handler;
            public TypedSubscription(string key, Action<object, T> handler) : base(key)
            {
                if (handler == null) throw new ArgumentNullException(nameof(handler));
                _handler = handler;
            }

            public override void Invoke(object sender, object message)
            {
                if (!Alive) return;
                if (message == null || typeof(T).IsAssignableFrom(message.GetType()))
                {
                    _handler.Invoke(sender, (T)message);
                }
            }
        }
        public class ObjectSubscription : Subscription
        {
            private readonly Action<object, object> _handler;
            public ObjectSubscription(string key, Action<object, object> handler) : base(key)
            {
                if (handler == null) throw new ArgumentNullException(nameof(handler));
                _handler = handler;
            }


            public override void Invoke(object sender, object message)
            {
                if (!Alive) return;
                _handler.Invoke(sender, message);
            }
        }
        private ManualResetEventSlim _pendingMessageSignal = new ManualResetEventSlim(true);
        private readonly SemaphoreSlim _subscriptionSemaphore = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _queueSemaphore = new SemaphoreSlim(1, 1);
        Queue<Message> _messageQueue = new Queue<Message>();

        List<WeakReference> _subscriptions = new List<WeakReference>();
        static readonly Lazy<Messenger> LazyMessenger = new Lazy<Messenger>(() => new Messenger(), LazyThreadSafetyMode.ExecutionAndPublication);
        public static IMessenger Instance => LazyMessenger.Value;
        private Messenger()
        {
            _messageQueueProcessor = new Thread(ProcessMessageQueue);
            _messageQueueProcessor.Start();
        }

        private void ProcessMessageQueue(object obj)
        {
            while (true)
            {

                var signaled = _pendingMessageSignal.Wait(1000);
                Message next = null;
                _queueSemaphore.Wait();
                try
                {
                    if (!signaled && _messageQueue.Count == 0) continue;
                    else if (_messageQueue.Count == 0)
                    {
                        _pendingMessageSignal.Reset();
                    }
                    else
                    {
                        next = _messageQueue.Dequeue();
                    }
                }
                finally
                {
                    _queueSemaphore.Release();
                }
                if (next == null) continue;

                foreach (var part in Decompose(next.Key))
                {
                    List<Subscription> partSubscriptions;
                    _subscriptionSemaphore.Wait();
                    try
                    {
                        _subscriptions.RemoveAll(i => !i.IsAlive);
                        partSubscriptions = _subscriptions.Select(i => i.IsAlive ? i.Target as Subscription : null)
                                                          .Where(i => i != null)
                                                          .Where(i => string.Equals(i.Key, part, StringComparison.OrdinalIgnoreCase))
                                                          .ToList();
                    }
                    finally
                    {
                        _subscriptionSemaphore.Release();
                    }
                    foreach (var subscription in partSubscriptions)
                    {
                        try
                        {
                            subscription.Invoke(next.Sender, next.Data);
                        }
                        catch (Exception ex)
                        {
                            Publish("Messenger.Internal.Exception", this, ex);
                        }
                    }
                }
            }
        }

        private IEnumerable<string> Decompose(string key)
        {
            var parts = key.Split(".");
            for (var x = 0; x < parts.Length; x++)
            {
                yield return string.Join('.', parts, 0, parts.Length - x);
            }
        }

        public void Publish(string key, object sender, object data)
        {
            _queueSemaphore.Wait();
            try
            {
                PublishImpl(key, sender, data);
            }
            finally
            {
                _queueSemaphore.Release();
            }
        }

        public async Task PublishAsync(string key, object sender, object data, CancellationToken cancellationToken = default(CancellationToken))
        {
            await _queueSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                PublishImpl(key, sender, data);
            }
            finally
            {
                _queueSemaphore.Release();
            }
        }

        private void PublishImpl(string key, object sender, object data)
        {
            _messageQueue.Enqueue(new Message(key, sender, data));
        }

        public IDisposable Subscribe(string key, Action<object, object> callback)
        {
            var sub = new ObjectSubscription(key, callback);
            Subscribe(sub);
            return sub;
        }

        private void Subscribe(Subscription subscription)
        {
            _subscriptionSemaphore.Wait();
            try
            {
                _subscriptions.Add(new WeakReference(subscription));
            }
            finally
            {
                _subscriptionSemaphore.Release();
            }
        }

        public IDisposable Subscribe<T>(string key, Action<object, T> callback) where T : class
        {
            var sub = new TypedSubscription<T>(key, callback);
            Subscribe(sub);
            return sub;
        }
    }
}