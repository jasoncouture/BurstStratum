using System;

namespace BurstStratum.Services
{
    public class HeartbeatStratumMessage : StratumMessage
    {
        public HeartbeatStratumMessage() : base(MessageType.Heartbeat)
        {
            this.AddField(DateTimeOffset.Now.ToUnixTimeSeconds());
        }
    }
}