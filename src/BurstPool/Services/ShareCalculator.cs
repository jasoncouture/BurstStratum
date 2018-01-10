using BurstPool.Services.Interfaces;

namespace BurstPool.Services
{
    public class ShareCalculator : IShareCalculator
    {
        // I don't actually care if this is right or not, it just lets me calculate a weighted amount based on baseTarget.
        private const decimal GenesisBlockDifficulty = 18325193796UL;
        // 240 is the block target, so 240s = 1 raw share, less than 240 = more raw shares, more = less.
        private decimal GetBaseDeadlineScore(ulong deadline)
        {
            return 240m / (decimal)deadline;
        }
        // Again, don't actually care if this is right, it's right for the purpose of distributing shares.
        public decimal GetDifficulty(ulong baseTarget)
        {
            return GenesisBlockDifficulty / (decimal)baseTarget;
        }
        // Tie the two together.
        public decimal GetShares(ulong deadline, ulong baseTarget)
        {
            return GetBaseDeadlineScore(deadline) * GetDifficulty(baseTarget);
        }
    }
}