using DatabaseMigrator.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Hangfire;

namespace DatabaseMigrator.Pages;

public class MigrateModel : PageModel
{
    private readonly IBackgroundJobClient _backgroundJobs;

    public MigrateModel(IBackgroundJobClient backgroundJobs)
    {
        _backgroundJobs = backgroundJobs;
    }

    public IActionResult OnPost()
    {
        var jobId = _backgroundJobs.Enqueue<MigrationService>(x => x.InitializeMigration());
        TempData["JobId"] = jobId;
        return RedirectToPage("/Migrate");
    }
}