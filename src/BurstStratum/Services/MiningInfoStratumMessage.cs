using BurstStratum.Models;

namespace BurstStratum.Services
{
    public class MiningInfoStratumMessage : StratumMessage{
        public MiningInfoStratumMessage(MiningInfo miningInfo) : base(MessageType.MiningInfo) {
            this.AddField(miningInfo.GenerationSignature.ToByteArray())
                .AddField(ulong.Parse(miningInfo.BaseTarget))
                .AddField(ulong.Parse(miningInfo.Height))
                .AddField(miningInfo.TargetDeadline);
        }
    }
}