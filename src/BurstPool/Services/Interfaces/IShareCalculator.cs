using System;
using System.Threading;
using System.Threading.Tasks;

namespace BurstPool.Services.Interfaces
{
    public interface IShareCalculator
    {
         decimal GetShares(ulong deadline, ulong baseTarget);
         decimal GetDifficulty(ulong baseTarget);
    }

    public interface IMessenger {
        IDisposable Subscribe(string key, Action<object, object> callback);
        IDisposable Subscribe<T>(string key, Action<object, T> callback) where T : class;
        void Publish(string key, object sender = null, object data = null);
        Task PublishAsync(string key, object sender = null, object data = null, CancellationToken cancellationToken = default(CancellationToken));
    }

}