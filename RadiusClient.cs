using Flexinets.Radius.Core;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Flexinets.Radius
{
    public class RadiusClient
    {
        private readonly UdpClient _udpClient;
        private readonly RadiusDictionary _dictionary;

        public RadiusClient(IPEndPoint localEndpoint, RadiusDictionary dictionary)
        {
            _udpClient = new UdpClient(localEndpoint);
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

            await _udpClient.SendAsync(packetBytes, packetBytes.Length, remoteEndpoint);
            Task.Run(() =>
            {
                Thread.Sleep(timeout);
                _udpClient?.Close();
            });
            // todo use events to create a common udpclient for multiple packets to enable sending and receiving without blocking
            var response = await _udpClient.ReceiveAsync();
            return RadiusPacket.Parse(response.Buffer, _dictionary, packet.SharedSecret);
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
