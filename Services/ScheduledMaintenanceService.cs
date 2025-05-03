// Services/ScheduledMaintenanceService.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using RadXPriceBot.Data;

namespace RadXPriceBot.Services
{
    public class ScheduledMaintenanceService
    {
        private readonly DatabaseService _dbService;
        private readonly BotSettings _settings;
        private readonly Action<string> _logger;
        private Timer _maintenanceTimer;
        
        public ScheduledMaintenanceService(DatabaseService dbService, BotSettings settings, Action<string> logger = null)
        {
            _dbService = dbService;
            _settings = settings;
            _logger = logger ?? (msg => Console.WriteLine(msg));
        }
        
        public void Start()
        {
            try
            {
                _logger("Starting scheduled database maintenance service...");
                
                // Run maintenance once a day
                var interval = TimeSpan.FromDays(1).TotalMilliseconds;
                
                // First run in 1 hour after startup
                var firstRunDelay = TimeSpan.FromHours(1).TotalMilliseconds;
                
                _maintenanceTimer = new Timer(async _ => await RunMaintenanceAsync(), 
                                            null, 
                                            (int)firstRunDelay, 
                                            (int)interval);
                
                _logger("Scheduled maintenance service started");
            }
            catch (Exception ex)
            {
                _logger($"Error starting maintenance service: {ex.Message}");
            }
        }
        
        public void Stop()
        {
            try
            {
                _logger("Stopping scheduled maintenance service...");
                _maintenanceTimer?.Dispose();
                _maintenanceTimer = null;
                _logger("Scheduled maintenance service stopped");
            }
            catch (Exception ex)
            {
                _logger($"Error stopping maintenance service: {ex.Message}");
            }
        }
        
        private async Task RunMaintenanceAsync()
        {
            try
            {
                _logger("Running scheduled database maintenance...");
                
                // Clean up old data
                await _dbService.CleanupOldDataAsync(_settings.PriceHistoryRetentionDays);
                
                _logger("Scheduled maintenance completed successfully");
            }
            catch (Exception ex)
            {
                _logger($"Error during scheduled maintenance: {ex.Message}");
            }
        }
    }
}
