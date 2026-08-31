using System;
using System.Threading;
using System.Threading.Tasks;
using ClipboardManager.Data;

namespace ClipboardManager.Services
{
    public interface IExpirationCleanupService : IDisposable
    {
        void Start(TimeSpan sweepInterval);
        void Stop();
        Task SweepAsync();
    }

    public class ExpirationCleanupService : IExpirationCleanupService
    {
        private readonly IClipboardRepository _repository;
        private Timer? _timer;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public ExpirationCleanupService(IClipboardRepository repository)
        {
            _repository = repository;
        }

        public void Start(TimeSpan sweepInterval)
        {
            _timer?.Dispose();
            _timer = new Timer(async _ => await SweepAsync(), null, sweepInterval, sweepInterval);
        }

        public void Stop()
        {
            _timer?.Change(Timeout.Infinite, 0);
        }

        public async Task SweepAsync()
        {
            if (!await _semaphore.WaitAsync(0))
            {
                return; // Skip if a sweep is already in progress
            }

            try
            {
                await _repository.DeleteExpiredAsync();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _semaphore.Dispose();
        }
    }
}
