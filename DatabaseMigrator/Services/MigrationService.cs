// using Dapper;
// using Npgsql;
// using System.Data.SqlClient;
// using Microsoft.Extensions.Logging;
// using Hangfire;
// using System.ComponentModel;
// using Hangfire.Server;

// namespace DatabaseMigrator.Services
// {
//     public class MigrationService
//     {
//         private readonly ILogger<MigrationService> _logger;
//         private const int BatchSize = 10000;

//         public MigrationService(ILogger<MigrationService> logger)
//         {
//             _logger = logger;
//         }

//         [AutomaticRetry(Attempts = 3)]
//         [DisplayName("Migrate Customer Data")]
//         public void MigrateAll()  // Simplified back to no parameters
//         {
//             try
//             {
//                 _logger.LogInformation("Starting migration...");
                
//                 // Source: SQL Server
//                 using var sourceConn = new SqlConnection(
//                     "Server=localhost;Database=ECommerceSource;User Id=sa;Password=Str0ngPass!;");
                
//                 var customers = sourceConn.Query<Customer>("SELECT * FROM Customers").ToList();
//                 _logger.LogInformation("Fetched {Count} records", customers.Count);

//                 // Target: PostgreSQL
//                 using var targetConn = new NpgsqlConnection(
//                     "Host=localhost;Database=ecommercetarget;Username=postgres;Password=postgres;Include Error Detail=true;CommandTimeout=300;");
                
//                 targetConn.Open();

//                 using var transaction = targetConn.BeginTransaction();
//                 try
//                 {
//                     _logger.LogInformation("Clearing existing data...");
//                     targetConn.Execute("TRUNCATE TABLE customers RESTART IDENTITY CASCADE", 
//                         transaction: transaction);

//                     _logger.LogInformation("Inserting new data in batches...");
//                     var batches = customers.Select((c, i) => new { Customer = c, Index = i })
//                                          .GroupBy(x => x.Index / BatchSize)
//                                          .Select(g => g.Select(x => x.Customer))
//                                          .ToList();

//                     var totalBatches = batches.Count;
//                     int batchNumber = 1;

//                     foreach (var batch in batches)
//                     {
//                         var progress = (double)batchNumber / totalBatches * 100;
//                         _logger.LogInformation("Processing batch {Current}/{Total} ({Progress:F2}%)", 
//                             batchNumber, totalBatches, progress);

//                         targetConn.Execute(@"
//                             INSERT INTO customers 
//                             (id, name, email, mobile, address, duebillamount)
//                             VALUES (@Id, @Name, @Email, @Mobile, @Address, @DueBillAmount)", 
//                             batch,
//                             transaction: transaction);
                        
//                         batchNumber++;
//                     }
                    
//                     transaction.Commit();
//                     _logger.LogInformation("Migration completed successfully!");
//                 }
//                 catch (Exception)
//                 {
//                     _logger.LogWarning("Rolling back transaction due to error");
//                     transaction.Rollback();
//                     throw;
//                 }
//             }
//             catch (Exception ex)
//             {
//                 _logger.LogError(ex, "Migration failed");
//                 throw;
//             }
//         }

//         private class Customer
//         {
//             public int Id { get; set; }
//             public string Name { get; set; } = null!;
//             public string Email { get; set; } = null!;
//             public string Mobile { get; set; } = null!;
//             public string Address { get; set; } = null!;
//             public decimal DueBillAmount { get; set; }
//         }
//     }
// }

using Dapper;
using Npgsql;
using System.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Hangfire;
using System.ComponentModel;

namespace DatabaseMigrator.Services
{
    public class MigrationService
    {
        private readonly ILogger<MigrationService> _logger;
        private const int BatchSize = 10000;

        public MigrationService(ILogger<MigrationService> logger)
        {
            _logger = logger;
        }

        [AutomaticRetry(Attempts = 3)]
        [DisplayName("Initialize Migration")]
        public void InitializeMigration()
        {
            try
            {
                _logger.LogInformation("Starting migration initialization...");
                
                // Source: SQL Server
                using var sourceConn = new SqlConnection(
                    "Server=localhost;Database=ECommerceSource;User Id=sa;Password=Str0ngPass!;");
                
                var totalRecords = sourceConn.ExecuteScalar<int>("SELECT COUNT(*) FROM Customers");
                _logger.LogInformation("Total records to migrate: {Count}", totalRecords);

                // Calculate total batches
                var totalBatches = (int)Math.Ceiling(totalRecords / (double)BatchSize);

                // Clear target table first
                using var targetConn = new NpgsqlConnection(
                    "Host=localhost;Database=ecommercetarget;Username=postgres;Password=postgres;Include Error Detail=true;CommandTimeout=300;");
                targetConn.Execute("TRUNCATE TABLE customers RESTART IDENTITY CASCADE");

                // Queue batch jobs
                for (int batchNumber = 0; batchNumber < totalBatches; batchNumber++)
                {
                    var offset = batchNumber * BatchSize;
                    BackgroundJob.Enqueue(() => ProcessBatch(batchNumber, offset, BatchSize));
                }

                _logger.LogInformation("Successfully queued {Count} batch jobs", totalBatches);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize migration");
                throw;
            }
        }

        [AutomaticRetry(Attempts = 3)]
        [DisplayName("Process Customer Batch")]
        public void ProcessBatch(int batchNumber, int offset, int batchSize)
        {
            try
            {
                _logger.LogInformation("Starting batch {BatchNumber} (Offset: {Offset})", batchNumber, offset);

                // Source: SQL Server
                using var sourceConn = new SqlConnection(
                    "Server=localhost;Database=ECommerceSource;User Id=sa;Password=Str0ngPass!;");

                var customers = sourceConn.Query<Customer>(
                    "SELECT * FROM Customers ORDER BY Id OFFSET @Offset ROWS FETCH NEXT @BatchSize ROWS ONLY",
                    new { Offset = offset, BatchSize = batchSize }
                ).ToList();

                _logger.LogInformation("Batch {BatchNumber}: Fetched {Count} records", batchNumber, customers.Count);

                if (customers.Any())
                {
                    // Target: PostgreSQL
                    using var targetConn = new NpgsqlConnection(
                        "Host=localhost;Database=ecommercetarget;Username=postgres;Password=postgres;Include Error Detail=true;CommandTimeout=300;");

                    targetConn.Open();
                    using var transaction = targetConn.BeginTransaction();

                    try
                    {
                        targetConn.Execute(@"
                            INSERT INTO customers 
                            (id, name, email, mobile, address, duebillamount)
                            VALUES (@Id, @Name, @Email, @Mobile, @Address, @DueBillAmount)",
                            customers,
                            transaction: transaction);

                        transaction.Commit();
                        _logger.LogInformation("Batch {BatchNumber}: Successfully processed {Count} records", 
                            batchNumber, customers.Count);
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process batch {BatchNumber}", batchNumber);
                throw;
            }
        }

        private class Customer
        {
            public int Id { get; set; }
            public string Name { get; set; } = null!;
            public string Email { get; set; } = null!;
            public string Mobile { get; set; } = null!;
            public string Address { get; set; } = null!;
            public decimal DueBillAmount { get; set; }
        }
    }
}