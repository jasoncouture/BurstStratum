using System;
using System.Collections.Generic;
using System.Linq;

namespace BurstStratum.Services
{
    public static class MessageBuffer
    {
        public static byte[] CreateMessageBuffer(MessageType type, int fieldCount, byte[] fieldData)
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
        public static byte[] MergeFields(params byte[][] data)
        {
            List<byte> buffer = new List<byte>();
            foreach (var param in data)
            {
                buffer.AddRange(param);
            }
            return buffer.ToArray();
        }

        public static byte[] CreateField(byte[] data)
        {
            var buffer = new byte[data.Length + 1];
            buffer[0] = (byte)data.Length;
            Buffer.BlockCopy(data, 0, buffer, 1, data.Length);
            return buffer;
        }
        public static byte[] CreateField(long data)
        {
            var dataBytes = BitConverter.GetBytes(data);
            if (!BitConverter.IsLittleEndian)
                dataBytes = dataBytes.Reverse().ToArray();
            return CreateField(dataBytes);
        }

        public static byte[] CreateField(ulong data)
        {
            var dataBytes = BitConverter.GetBytes(data);
            if (!BitConverter.IsLittleEndian)
                dataBytes = dataBytes.Reverse().ToArray();
            return CreateField(dataBytes);
        }
    }
}