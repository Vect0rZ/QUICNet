﻿using QuickNet.Utilities;
using QuicNet.Connections;
using QuicNet.Context;
using QuicNet.Exceptions;
using QuicNet.Infrastructure.Frames;
using QuicNet.Infrastructure.PacketProcessing;
using QuicNet.Infrastructure.Packets;
using QuicNet.Infrastructure.Settings;
using QuicNet.InternalInfrastructure;
using QuicNet.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace QuicNet
{
    /// <summary>
    /// Quic Client. Used for sending and receiving data from a Quic Server.
    /// </summary>
    public class QuicClient
    {
        private IPEndPoint _peerIp;
        private UdpClient _client;

        private QuicConnection _connection;
        private QuicStream _stream;

        private Unpacker _unpacker;
        private InitialPacketCreator _packetCreator;

        private UInt64 _maximumStreams = QuicSettings.MaximumStreamId;
        private PacketWireTransfer _pwt;

        public QuicClient()
        {
            _client = new UdpClient();
            _unpacker = new Unpacker();
            _packetCreator = new InitialPacketCreator();
        }

        /// <summary>
        /// Connect to a remote server.
        /// </summary>
        /// <param name="ip">Ip Address</param>
        /// <param name="port">Port</param>
        /// <returns></returns>
        public QuicConnection Connect(string ip, int port)
        {
            // Establish socket connection
            _peerIp = new IPEndPoint(IPAddress.Parse(ip), port);

            // Initialize packet reader
            _pwt = new PacketWireTransfer(_client, _peerIp);

            // Start initial protocol process
            InitialPacket connectionPacket = _packetCreator.CreateInitialPacket(0, 0);

            // Send the initial packet
            _pwt.SendPacket(connectionPacket);

            // Await response for sucessfull connection creation by the server
            InitialPacket packet = (InitialPacket)_pwt.ReadPacket();

            HandleInitialFrames(packet);
            EstablishConnection(packet.SourceConnectionId, packet.SourceConnectionId);

            return _connection;
        }

        /// <summary>
        /// Connect to a remote server.
        /// </summary>
        /// <param name="ip">Ip Address</param>
        /// <param name="port">Port</param>
        /// <returns></returns>
        public async Task<QuicConnection> ConnectAsync(string ip, int port)
        {
            // Establish socket connection
            _peerIp = new IPEndPoint(IPAddress.Parse(ip), port);

            // Initialize packet reader
            _pwt = new PacketWireTransfer(_client, _peerIp);

            // Start initial protocol process
            InitialPacket connectionPacket = _packetCreator.CreateInitialPacket(0, 0);

            // Send the initial packet
            await _pwt.SendPacketAsync(connectionPacket);

            // Await response for sucessfull connection creation by the server
            InitialPacket packet = (InitialPacket)await _pwt.ReadPacketAsync();

            HandleInitialFrames(packet);
            EstablishConnection(packet.SourceConnectionId, packet.SourceConnectionId);

            return _connection;
        }

        /// <summary>
        /// Handles initial packet's frames. (In most cases protocol frames)
        /// </summary>
        /// <param name="packet"></param>
        private void HandleInitialFrames(Packet packet)
        {
            List<Frame> frames = packet.GetFrames();
            for (int i = frames.Count - 1; i > 0; i--)
            {
                Frame frame = frames[i];
                if (frame is ConnectionCloseFrame)
                {
                    ConnectionCloseFrame ccf = (ConnectionCloseFrame)frame;

                    throw new QuicConnectivityException(ccf.ReasonPhrase);
                }

                if (frame is MaxStreamsFrame)
                {
                    MaxStreamsFrame msf = (MaxStreamsFrame)frame;
                    _maximumStreams = msf.MaximumStreams.Value;
                }

                // Break out if the first Padding Frame has been reached
                if (frame is PaddingFrame)
                    break;
            }
        }

        /// <summary>
        /// Create a new connection
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="peerConnectionId"></param>
        private void EstablishConnection(UInt32 connectionId, UInt32 peerConnectionId)
        {
            ConnectionData connection = new ConnectionData(_pwt, connectionId, peerConnectionId);
            _connection = new QuicConnection(connection);
        }
    }
}
