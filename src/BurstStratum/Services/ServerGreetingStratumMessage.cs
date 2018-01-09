using System;

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
}