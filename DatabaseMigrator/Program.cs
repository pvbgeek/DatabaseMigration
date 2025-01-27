// using DatabaseMigrator.Services;
// using Hangfire;
// using Hangfire.SqlServer;
// using Hangfire.Dashboard;

// var builder = WebApplication.CreateBuilder(args);

// // Add services
// builder.Services.AddRazorPages();
// builder.Services.AddScoped<MigrationService>();

// // Hangfire configuration
// builder.Services.AddHangfire(config => 
//     config.SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
//         .UseSimpleAssemblyNameTypeSerializer()
//         .UseRecommendedSerializerSettings()
//         .UseSqlServerStorage(
//             "Server=localhost;Database=ECommerceSource;User Id=sa;Password=Str0ngPass!;",
//             new SqlServerStorageOptions 
//             { 
//                 SchemaName = "Hangfire",
//                 CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
//                 SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
//                 QueuePollInterval = TimeSpan.Zero
//             }
//         )
// );

// // Add Hangfire server
// builder.Services.AddHangfireServer(options =>
// {
//     options.WorkerCount = 1;  // Set to 1 since we're doing database migration
//     options.Queues = new[] { "default" };  // Specify which queue to process
// });

// var app = builder.Build();

// // Configure middleware
// if (!app.Environment.IsDevelopment())
// {
//     app.UseExceptionHandler("/Error");
//     app.UseHsts();
// }

// app.UseHttpsRedirection();
// app.UseStaticFiles();
// app.UseRouting();

// // Configure Hangfire dashboard with authorization
// app.UseHangfireDashboard("/hangfire", new DashboardOptions
// {
//     Authorization = new[] { new LocalRequestsOnlyAuthorizationFilter() },
//     IsReadOnlyFunc = (DashboardContext context) => false
// });

// app.MapRazorPages();

// app.Run();

using DatabaseMigrator.Services;
using Hangfire;
using Hangfire.SqlServer;
using Hangfire.Dashboard;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorPages();
builder.Services.AddScoped<MigrationService>();

// Hangfire configuration
builder.Services.AddHangfire(config => 
    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(
            "Server=localhost;Database=ECommerceSource;User Id=sa;Password=Str0ngPass!;",
            new SqlServerStorageOptions 
            { 
                SchemaName = "Hangfire",
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.Zero
            }
        )
);

// Add Hangfire server with multiple workers
builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 20;  // Increased to handle multiple jobs
    options.Queues = new[] { "default" };
});

var app = builder.Build();

// Configure middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Configure Hangfire dashboard
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new LocalRequestsOnlyAuthorizationFilter() }
});

app.MapRazorPages();

app.Run();