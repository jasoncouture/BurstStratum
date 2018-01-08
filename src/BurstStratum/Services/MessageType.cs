namespace BurstStratum.Services
{
    public enum MessageType : byte
    {
        Heartbeat = 0,
        MiningInfo = 1,
        Greeting = 2,
        DeadlineSubmission = 10,
        DeadlineSubmissionStatus = 11,
        DeadlineSubmissionResult = 12
    }
}