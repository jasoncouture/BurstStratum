using System;

namespace BurstPool.Services.Interfaces
{
    public interface IShareCalculator
    {
         decimal GetShares(ulong deadline, ulong baseTarget);
         decimal GetDifficulty(ulong baseTarget);
    }
}