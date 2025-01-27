using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Hangfire;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using DatabaseMigrator.Services;

namespace DatabaseMigrator.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IBackgroundJobClient _backgroundJob;
    public int ActiveJobs { get; private set; }

    public IndexModel(ILogger<IndexModel> logger, IBackgroundJobClient backgroundJob)
    {
        _logger = logger;
        _backgroundJob = backgroundJob;
    }

    public void OnGet()
    {
        try
        {
            var monitor = JobStorage.Current.GetMonitoringApi();
            ActiveJobs = monitor.ProcessingJobs(0, int.MaxValue).Count();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching job statistics");
            ActiveJobs = 0;
        }
    }
}