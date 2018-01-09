using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BurstStratum.Configuration;
using BurstStratum.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StrangeSoft.Burst;

namespace BurstStratum.Services
{
    public class TcpStratumServer : BackgroundJob
    {
        

        public static byte[] CreateMiningInfoBuffer(MiningInfo miningInfo)
        {
            return new MiningInfoStratumMessage(miningInfo).Build();
        }
        public class TcpStratumClient
        {
            CancellationToken _cancellationToken;
            Socket _client;
            public TcpStratumClient(CancellationToken cancellationToken, Socket client)
            {
                _client = client;
                _cancellationToken = cancellationToken;
                _lastSend = DateTimeOffset.Now.ToUnixTimeSeconds();
                _timer = new Timer(OnTimerTick, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
            }

            private void OnTimerTick(object state)
            {
                if (DateTimeOffset.Now.ToUnixTimeSeconds() - _lastSend > 30)
                {
                    SendHeartbeat();
                }
            }


            Timer _timer;

            private bool _connected = true;
            public bool Connected
            {
                get => _connected; private set
                {
                    if (_connected == value) return;
                    _connected = value;
                    if (!_connected)
                        OnDisconnected();
                }
            }

            private SemaphoreSlim _socketSemaphore = new SemaphoreSlim(1, 1);
            public async void SendBytes(byte[] buffer)
            {
                if (!Connected) return;
                _lastSend = DateTimeOffset.Now.ToUnixTimeSeconds();
                await _socketSemaphore.WaitAsync(_cancellationToken).ConfigureAwait(false);
                try
                {
                    _client.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, EndSendBytes, null);
                }
                catch (Exception ex)
                {
                    OnSocketError(ex);
                    _socketSemaphore.Release();
                }
            }
            public event EventHandler Disconnected;
            private void OnDisconnected()
            {
                try
                {
                    _client.Close();
                }
                catch
                {
                    // Ignored
                }
                try
                {
                    _client.Dispose();
                }
                catch
                {
                    // Ignored.
                }

                Disconnected?.Invoke(this, EventArgs.Empty);
            }
            private void OnSocketError(Exception ex)
            {
                if (!Connected) return;
                Connected = false;
            }

            private void EndSendBytes(IAsyncResult asyncResult)
            {
                try
                {
                    _client.EndSend(asyncResult);
                }
                catch
                {
                    try
                    {
                        _client.Close();
                    }
                    catch
                    {
                        // Ignored.
                    }
                }
                finally
                {
                    _socketSemaphore.Release();
                }
            }
            public void SendMessage(IStratumMessage message) 
            {
                SendBytes(message.Build());
            }
            private void SendHeartbeat()
            {
                SendMessage(new HeartbeatStratumMessage());
                // var buffer = CreateMessageBuffer(MessageType.Heartbeat, 1, CreateField(DateTimeOffset.Now.ToUnixTimeSeconds()));
                // SendBytes(buffer);
            }
            long _lastSend;

            internal void DisconnectClient()
            {
                if (!Connected) return;
                Connected = false;
            }
        }
        private ILogger _logger;
        private StratumSettings _settings;
        private IMiningInfoPoller _poller;
        public TcpStratumServer(IConfiguration configuration, IMiningInfoPoller poller, ILoggerFactory loggerFactory)
        {
            var settings = new StratumSettings();
            configuration.Bind("Stratum", settings);
            _settings = settings;
            _poller = poller;
            _poller.MiningInfoChanged += OnMiningInfoChanged;
            _logger = loggerFactory.CreateLogger<TcpStratumServer>();
        }

        private async void OnMiningInfoChanged(object sender, EventArgs e)
        {
            List<TcpStratumClient> clientSnapshot = new List<TcpStratumClient>();
            await clientSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                clientSnapshot.AddRange(clients.ToArray());

            }
            finally
            {
                clientSemaphore.Release();
            }
            var buffer = CreateMiningInfoBuffer(await _poller.GetCurrentMiningInfoAsync());
            Parallel.ForEach(clients, client =>
            {
                client.SendBytes(buffer);
            });
        }

        List<TcpStratumClient> clients = new List<TcpStratumClient>();
        SemaphoreSlim clientSemaphore = new SemaphoreSlim(1, 1);
        protected override async Task ExecuteAsync(CancellationToken stopCancellationToken)
        {
            if (_settings.TcpPort <= 0) return;
            var server = new TcpListener(IPAddress.Any, _settings.TcpPort);
            server.Server.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
            server.Start(1024);
            stopCancellationToken.Register(() =>
            {
                try
                {
                    server.Stop();
                }
                catch
                {

                }
            });
            List<Task> acceptTasks = new List<Task>();
            while (!stopCancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (server.Pending())
                    {
                        var socket = await server.AcceptSocketAsync().ConfigureAwait(false);
                        _logger.LogInformation($"Accepted new connection");
                        var client = new TcpStratumClient(stopCancellationToken, socket);
                        acceptTasks.Add(Task.Factory.StartNew(async () =>
                        {
                            try
                            {
                                client.SendMessage(new ServerGreetingStratumMessage());
                                // client.SendMessage(new StratumMessage(MessageType.Greeting)
                                //     .AddField(StratumBinaryProtocolVersion)
                                //     .AddField($"BurstStratum/{Environment.OSVersion.Platform}")
                                //     .AddField(DateTimeOffset.Now.ToUnixTimeSeconds())
                                //     );
                                // client.SendBytes(CreateMessageBuffer(MessageType.Greeting, 3, 
                                // MergeFields(CreateField(StratumBinaryProtocolVersion), 
                                // CreateField(Encoding.UTF8.GetBytes($"BurstStratum/{Environment.OSVersion.Platform}")), 
                                // CreateField(DateTimeOffset.Now.ToUnixTimeSeconds()))));
                                await clientSemaphore.WaitAsync(stopCancellationToken).ConfigureAwait(false);
                                try
                                {
                                    clients.Add(client);
                                }
                                finally
                                {
                                    clientSemaphore.Release();
                                }
                                try
                                {
                                    client.SendBytes(CreateMiningInfoBuffer(await _poller.GetCurrentMiningInfoAsync()));
                                }
                                catch
                                {
                                    // Ignored.
                                }
                            }
                            catch
                            {
                                // Ignored.
                            }
                        }).Unwrap());
                        acceptTasks.RemoveAll(i => i.IsCompleted);
                    }
                    else
                    {
                        await Task.Delay(100);
                    }
                }
                catch (OperationCanceledException) when (stopCancellationToken.IsCancellationRequested)
                {
                    // Shutdown gracefully when requested.
                    try
                    {
                        server.Stop();
                    }
                    catch
                    {
                        // Ignored.
                    }
                    // Wait for all pending clients to be accepted.
                    await Task.WhenAll(acceptTasks);
                    List<TcpStratumClient> clientSnapshot = new List<TcpStratumClient>();
                    // Grab a copy of all clients
                    await clientSemaphore.WaitAsync();
                    try
                    {
                        clientSnapshot.AddRange(clients.ToArray());
                    }
                    finally
                    {
                        clientSemaphore.Release();
                    }
                    // Close all connections.
                    Parallel.ForEach(clientSnapshot, i => i.DisconnectClient());
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "An unhandled exception occured in the main server loop.");
                }
            }

        }
        private async void OnClientDisconnected(object sender, EventArgs e)
        {
            var client = (TcpStratumClient)sender;
            await clientSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                clients.Remove(client);
            }
            finally
            {
                clientSemaphore.Release();
            }
        }
    }
}