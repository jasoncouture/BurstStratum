using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using BurstStratum.Configuration;
using BurstStratum.Models;
using BurstStratum.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BurstStratum.Services
{
    public class TcpStratumServer : BackgroundJob
    {
        private static byte[] CreateMessageBuffer(MessageType type, int fieldCount, byte[] fieldData)
        {
            byte[] buffer = new byte[sizeof(ushort) + 1 + fieldData.Length];
            var fieldCountBytes = BitConverter.GetBytes((ushort)fieldCount);
            if (!BitConverter.IsLittleEndian)
                fieldCountBytes = fieldCountBytes.Reverse().ToArray();
            Buffer.BlockCopy(fieldCountBytes, 0, buffer, 0, fieldCountBytes.Length);
            buffer[2] = (byte)type;
            Buffer.BlockCopy(fieldData, 0, buffer, 3, fieldData.Length);
            return buffer;
        }
        private static byte[] MergeFields(params byte[][] data)
        {
            List<byte> buffer = new List<byte>();
            foreach (var param in data)
            {
                buffer.AddRange(param);
            }
            return buffer.ToArray();
        }
        public static byte[] CreateMiningInfoBuffer(MiningInfo miningInfo)
        {
            var baseTarget = CreateField(ulong.Parse(miningInfo.BaseTarget));
            var height = CreateField(ulong.Parse(miningInfo.Height));
            var target = CreateField(miningInfo.TargetDeadline);
            var genSig = CreateField(miningInfo.GenerationSignature.ToByteArray());
            return CreateMessageBuffer(MessageType.MiningInfo, 4, MergeFields(genSig, baseTarget, height, target));
        }
        private static byte[] CreateField(byte[] data)
        {
            var buffer = new byte[data.Length + 1];
            buffer[0] = (byte)data.Length;
            Buffer.BlockCopy(data, 0, buffer, 1, data.Length);
            return buffer;
        }
        private static byte[] CreateField(long data)
        {
            var dataBytes = BitConverter.GetBytes(data);
            if (!BitConverter.IsLittleEndian)
                dataBytes = dataBytes.Reverse().ToArray();
            return CreateField(dataBytes);
        }

        private static byte[] CreateField(ulong data)
        {
            var dataBytes = BitConverter.GetBytes(data);
            if (!BitConverter.IsLittleEndian)
                dataBytes = dataBytes.Reverse().ToArray();
            return CreateField(dataBytes);
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
                if (DateTimeOffset.Now.ToUnixTimeMilliseconds() - _lastSend > 30)
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
            private void SendHeartbeat()
            {
                var buffer = CreateMessageBuffer(MessageType.Heartbeat, 1, CreateField(DateTimeOffset.Now.ToUnixTimeSeconds()));
                SendBytes(buffer);
            }
            long _lastSend;


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

            await clientSemaphore.WaitAsync();
            try
            {
                var buffer = CreateMiningInfoBuffer(await _poller.GetCurrentMiningInfoAsync());
                Parallel.ForEach(clients, client =>
                {
                    client.SendBytes(buffer);
                });
            }
            finally
            {
                clientSemaphore.Release();
            }
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

            while (!stopCancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (server.Pending())
                    {
                        var socket = await server.AcceptSocketAsync().ConfigureAwait(false);
                        _logger.LogInformation($"Accepted new connection");
                        var client = new TcpStratumClient(stopCancellationToken, socket);
                        await clientSemaphore.WaitAsync().ConfigureAwait(false);
                        try
                        {
                            clients.Add(client);
                            client.SendBytes(CreateMiningInfoBuffer(await _poller.GetCurrentMiningInfoAsync()));
                        }
                        catch
                        { // Ignored 
                        }
                        clientSemaphore.Release();
                    }
                    else
                    {
                        await Task.Delay(100);
                    }
                }
                catch
                {

                }
            }

        }
        private async void OnClientDisconnected(object sender, EventArgs e)
        {
            var client = (TcpStratumClient)sender;
            await clientSemaphore.WaitAsync();
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