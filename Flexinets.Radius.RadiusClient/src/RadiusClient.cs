using System;
using Flexinets.Radius.Core;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Flexinets.Radius
{
    public class RadiusClient : IDisposable
    {
        private readonly IRadiusPacketParser _radiusPacketParser;
        private readonly UdpClient _udpClient;
        private Task _receiveLoopTask;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private readonly
            ConcurrentDictionary<(byte Identifier, IPEndPoint RemoteEndpoint), TaskCompletionSource<UdpReceiveResult>>
            _pendingRequests =
                new ConcurrentDictionary<(byte Identifier, IPEndPoint RemoteEndpoint),
                    TaskCompletionSource<UdpReceiveResult>>();


        /// <summary>
        /// Create a radius client which sends and receives responses on localEndpoint
        /// </summary>
        public RadiusClient(IPEndPoint localEndpoint, IRadiusPacketParser radiusPacketParser)
        {
            _radiusPacketParser = radiusPacketParser;
            _udpClient = new UdpClient(localEndpoint);
        }


        /// <summary>
        /// Send a packet with default timeout of 3 seconds
        /// </summary>
        public async Task<IRadiusPacket> SendPacketAsync(IRadiusPacket packet, IPEndPoint remoteEndpoint) =>
            await SendPacketAsync(packet, remoteEndpoint, TimeSpan.FromSeconds(3));


        /// <summary>
        /// Send a packet with specified timeout
        /// </summary>
        public async Task<IRadiusPacket> SendPacketAsync(IRadiusPacket packet, IPEndPoint remoteEndpoint,
            TimeSpan timeout)
        {
            // Start a receive loop before sending packet if one isnt already running to ensure we can receive the response
            if (_receiveLoopTask != null)
            {
                _receiveLoopTask = Task.Factory.StartNew(
                    StartReceiveLoopAsync,
                    _cancellationTokenSource.Token,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
            }


            var completionSource = new TaskCompletionSource<UdpReceiveResult>();

            if (_pendingRequests.TryAdd((packet.Identifier, remoteEndpoint), completionSource))
            {
                var bytes = _radiusPacketParser.GetBytes(packet);
                await _udpClient.SendAsync(bytes, bytes.Length, remoteEndpoint);

                if (await Task.WhenAny(completionSource.Task, Task.Delay(timeout)) == completionSource.Task)
                {
                    return _radiusPacketParser.Parse(
                        completionSource.Task.Result.Buffer,
                        packet.SharedSecret,
                        packet.Authenticator);
                }

                if (_pendingRequests.TryRemove((packet.Identifier, remoteEndpoint), out var tcs))
                {
                    tcs.SetCanceled();
                }

                throw new InvalidOperationException(
                    $"Receive response for id {packet.Identifier} timed out after {timeout}");
            }

            throw new InvalidOperationException($"There is already a pending receive with id {packet.Identifier}");
        }


        /// <summary>
        /// Receive packets in a loop and complete tasks based on identifier
        /// </summary>
        private async Task StartReceiveLoopAsync()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    var response = await _udpClient.ReceiveAsync();
                    if (_pendingRequests.TryRemove((response.Buffer[1], response.RemoteEndPoint),
                            out var tcs))
                    {
                        tcs.SetResult(response);
                    }
                }
                catch (ObjectDisposedException) // This is thrown when udpclient is disposed, can be safely ignored
                {
                }
            }
        }


        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _cancellationTokenSource.Cancel();
            _receiveLoopTask?.Dispose();
            _udpClient.Dispose();
        }
    }
}