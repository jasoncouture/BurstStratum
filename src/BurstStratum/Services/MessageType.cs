namespace BurstStratum.Services
{
    public enum MessageType : byte
    {
        Heartbeat = 0,
        MiningInfo = 1,
        DeadlineSubmission = 2,
        DeadlineSubmissionStatus = 3,
        DeadlineSubmissionResult = 4
    }
}