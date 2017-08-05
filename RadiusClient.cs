using Flexinets.Radius.Core;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Flexinets.Radius
{
    public class RadiusClient
    {
        private readonly IPEndPoint _localEndpoint;
        private readonly RadiusDictionary _dictionary;


        /// <summary>
        /// Create a radius client which sends and receives responses on localEndpoint
        /// </summary>
        /// <param name="localEndpoint"></param>
        /// <param name="dictionary"></param>
        public RadiusClient(IPEndPoint localEndpoint, RadiusDictionary dictionary)
        {
            _localEndpoint = localEndpoint;
            _dictionary = dictionary;
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
            var packetBytes = packet.GetBytes(_dictionary);
            using (var udpClient = new UdpClient(_localEndpoint))
            {
                await udpClient.SendAsync(packetBytes, packetBytes.Length, remoteEndpoint);

                // todo use events to create a common udpclient for multiple packets to enable sending and receiving without blocking
                var responseTask = udpClient.ReceiveAsync();

                var completedTask = await Task.WhenAny(responseTask, Task.Delay(timeout));
                if (completedTask == responseTask)
                {
                    return RadiusPacket.Parse(responseTask.Result.Buffer, _dictionary, packet.SharedSecret);
                }                
                throw new TaskCanceledException($"Receive timed out after {timeout}");
            }
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
    }
}
