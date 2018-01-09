using System;
using System.Linq;
using System.Text;
using BurstStratum.Services.Interfaces;

namespace BurstStratum.Services
{
    public static class StratumMessageExtensions
    {
        public static IStratumMessage AddField(this IStratumMessage message, string data) {
            return message.AddField(data, Encoding.UTF8);
        }
        public static IStratumMessage AddField(this IStratumMessage message, string data, Encoding encoding) {
            return message.AddField(encoding.GetBytes(data));
        }
        public static IStratumMessage AddField(this IStratumMessage message, ulong data)
        {
            var dataBytes = BitConverter.GetBytes(data);
            if (!BitConverter.IsLittleEndian)
                dataBytes = dataBytes.Reverse().ToArray();
            return message.AddField(dataBytes);
        }

        public static IStratumMessage AddField(this IStratumMessage message, long data)
        {
            var dataBytes = BitConverter.GetBytes(data);
            if (!BitConverter.IsLittleEndian)
                dataBytes = dataBytes.Reverse().ToArray();
            return message.AddField(dataBytes);
        }
    }
}