using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace BurstStratum.Services
{
    public abstract class BackgroundJob : IHostedService, IDisposable
    {

        Task _backgroundTask;
        private CancellationTokenSource _stopCancellationTokenSource = new CancellationTokenSource();
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if(_stopCancellationTokenSource.IsCancellationRequested) {
                _stopCancellationTokenSource = new CancellationTokenSource();
            }
            var _backgroundTask = Task.Factory.StartNew(async () => await ExecuteAsync(_stopCancellationTokenSource.Token), TaskCreationOptions.LongRunning).Unwrap();
            if (_backgroundTask.IsCompleted)
            {
                return _backgroundTask;
            }
            return Task.CompletedTask;
        }

        protected abstract Task ExecuteAsync(CancellationToken stopCancellationToken);

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_backgroundTask == null) return;
            


            try
            {
                if (!_stopCancellationTokenSource.IsCancellationRequested)
                    _stopCancellationTokenSource.Cancel();
            }
            finally
            {
                await Task.WhenAny(_backgroundTask, Task.Delay(Timeout.Infinite,
                                              cancellationToken));
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _stopCancellationTokenSource?.Dispose();
                }

                disposedValue = true;
            }
        }


        // This code added to correctly implement the disposable pattern.
        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}