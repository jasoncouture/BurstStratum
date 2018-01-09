using System;
using System.Collections.Generic;
using System.Linq;
using BurstStratum.Services.Interfaces;

namespace BurstStratum.Services
{
    public class ServerGreetingStratumMessage : StratumMessage
    {
        public const ulong StratumBinaryProtocolVersion = 1;
        public ServerGreetingStratumMessage() : base(MessageType.Greeting)
        {
            this.AddField(StratumBinaryProtocolVersion)
                .AddField($"BurstStratum/{Environment.OSVersion.Platform}")
                .AddField(DateTimeOffset.Now.ToUnixTimeSeconds());
        }
    }
    public class HeartbeatStratumMessage : StratumMessage
    {
        public HeartbeatStratumMessage() : base(MessageType.Heartbeat)
        {
            this.AddField(DateTimeOffset.Now.ToUnixTimeSeconds());
        }
    }
    public class StratumMessage : IStratumMessage
    {
        readonly MessageType _messageType;
        readonly List<byte[]> _fields = new List<byte[]>();
        public StratumMessage(MessageType messageType)
        {
            _messageType = messageType;
        }

        public IStratumMessage AddField(byte[] data)
        {
            _fields.Add(MessageBuffer.CreateField(data));
            return this;
        }

        public byte[] Build()
        {
            return MessageBuffer.CreateMessageBuffer(_messageType, _fields.Count, MessageBuffer.MergeFields(_fields.ToArray()));
        }



    }
}