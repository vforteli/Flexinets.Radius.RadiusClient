﻿using Flexinets.Radius.Core;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Flexinets.Radius
{
    public class RadiusClient : IDisposable
    {
        private readonly IPEndPoint _localEndpoint;
        private readonly UdpClient _udpClient;
        private readonly IRadiusPacketParser _radiusPacketParser;
        private readonly ConcurrentDictionary<(Byte identifier, IPEndPoint remoteEndpoint), TaskCompletionSource<UdpReceiveResult>> _pendingRequests = new ConcurrentDictionary<(Byte, IPEndPoint), TaskCompletionSource<UdpReceiveResult>>();


        /// <summary>
        /// Create a radius client which sends and receives responses on localEndpoint
        /// </summary>
        /// <param name="localEndpoint"></param>
        /// <param name="dictionary"></param>
        public RadiusClient(IPEndPoint localEndpoint, IRadiusPacketParser radiusPacketParser)
        {
            _localEndpoint = localEndpoint;
            _radiusPacketParser = radiusPacketParser;
            _udpClient = new UdpClient(_localEndpoint);
            var receiveTask = StartReceiveLoopAsync();
        }


        /// <summary>
        /// Send a packet with specified timeout
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="remoteEndpoint"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public async Task<IRadiusPacket> SendPacketAsync(IRadiusPacket packet, IPEndPoint remoteEndpoint, TimeSpan timeout)
        {
            var packetBytes = _radiusPacketParser.GetBytes(packet);
            var responseTaskCS = new TaskCompletionSource<UdpReceiveResult>();
            if (_pendingRequests.TryAdd((packet.Identifier, remoteEndpoint), responseTaskCS))
            {
                await _udpClient.SendAsync(packetBytes, packetBytes.Length, remoteEndpoint);
                var completedTask = await Task.WhenAny(responseTaskCS.Task, Task.Delay(timeout));
                if (completedTask == responseTaskCS.Task)
                {
                    return _radiusPacketParser.Parse(responseTaskCS.Task.Result.Buffer, packet.SharedSecret);
                }
                if (_pendingRequests.TryRemove((packet.Identifier, remoteEndpoint), out var taskCS))
                {
                    taskCS.SetCanceled();
                }
                throw new InvalidOperationException($"Receive response for id {packet.Identifier} timed out after {timeout}");
            }
            throw new InvalidOperationException($"There is already a pending receive with id {packet.Identifier}");
        }


        /// <summary>
        /// Send a packet with default timeout of 3 seconds
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="remoteEndpoint"></param>
        /// <returns></returns>
        public async Task<IRadiusPacket> SendPacketAsync(IRadiusPacket packet, IPEndPoint remoteEndpoint)
        {
            return await SendPacketAsync(packet, remoteEndpoint, TimeSpan.FromSeconds(3));
        }


        /// <summary>
        /// Receive packets in a loop and complete tasks based on identifier
        /// </summary>
        /// <returns></returns>
        private async Task StartReceiveLoopAsync()
        {
            while (true)    // Maybe this should be started and stopped when there are pending responses
            {
                try
                {
                    var response = await _udpClient.ReceiveAsync();
                    if (_pendingRequests.TryRemove((response.Buffer[1], response.RemoteEndPoint), out var taskCS))
                    {
                        taskCS.SetResult(response);
                    }
                }
                catch (ObjectDisposedException)
                {
                    // This is thrown when udpclient is disposed, can be safely ignored
                    return;
                }
            }
        }


        public void Dispose()
        {
            _udpClient?.Dispose();
        }
    }
}
