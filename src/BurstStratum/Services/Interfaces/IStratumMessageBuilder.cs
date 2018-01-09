namespace BurstStratum.Services.Interfaces
{
    public interface IStratumMessage {
        IStratumMessage AddField(byte[] data);
        byte[] Build();
    }
}