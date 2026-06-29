public class DailyTask : IHostedService, IDisposable
{
    private Timer _timer;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public DailyTask(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromDays(0));
        return Task.CompletedTask;
    }

    private void DoWork(object state)
    {
        var currentTime = DateTime.Now;
        // Check if current time matches any of the three desired times
        if ((currentTime.Hour == 5 && currentTime.Minute == 0) ||
            (currentTime.Hour == 18 && currentTime.Minute == 0))
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var databaseService = scope.ServiceProvider.GetRequiredService<DatabaseService>();
                databaseService.ClearFoldersTable();
                databaseService.PopulateAndCheckFolders();
                databaseService.CreateOrUpdateDatabase();
            }
        }
    }


    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}