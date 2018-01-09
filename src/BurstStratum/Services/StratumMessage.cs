using System.Collections.Generic;
using System.Linq;
using BurstStratum.Services.Interfaces;

namespace BurstStratum.Services
{
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