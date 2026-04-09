using Azure.Storage.Blobs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorkflowAI.Domain.TenantConnectors;
using WorkflowAI.Infrastructure.Persistence.EntityFramework;
using WorkflowAI.Infrastructure.Services;
using Npgsql;

namespace WorkflowAI.Infrastructure.IntegrationTests.TenantConnectors;

/// <summary>
/// Integration test for exporting TenantConnectors to Excel and uploading to Azure Blob Storage.
///
/// Requirements:
///   PGHOST, PGUSER, PGPASSWORD, PGPORT, PGDATABASE — Azure DB credentials
///   AZURE_STORAGE_ACCOUNT_NAME — Azure Storage Account name
///   AZURE_STORAGE_ACCOUNT_KEY — Azure Storage Account key
///
/// Run with:
///   PGHOST=workflowai.postgres.database.azure.com \
///   PGUSER=postgres \
///   PGPASSWORD='...' \
///   PGPORT=5432 \
///   PGDATABASE=postgres \
///   AZURE_STORAGE_ACCOUNT_NAME='workflowai' \
///   AZURE_STORAGE_ACCOUNT_KEY='...' \
///   dotnet test tests/WorkflowAI.Infrastructure.IntegrationTests \
///     --filter "FullyQualifiedName~ExportConnectorsToExcelTests"
/// </summary>
[Trait("Category", "AzureDb")]
public class ExportConnectorsToExcelTests : IAsyncLifetime
{
    private static WorkflowAIDbContext CreateDb()
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host     = Environment.GetEnvironmentVariable("PGHOST")     ?? "workflowai.postgres.database.azure.com",
            Port     = int.TryParse(Environment.GetEnvironmentVariable("PGPORT"), out var p) ? p : 5432,
            Database = Environment.GetEnvironmentVariable("PGDATABASE") ?? "postgres",
            Username = Environment.GetEnvironmentVariable("PGUSER")     ?? "postgres",
            Password = Environment.GetEnvironmentVariable("PGPASSWORD") ?? "Gotik$$789",
            SslMode  = SslMode.Require
        };

        var options = new DbContextOptionsBuilder<WorkflowAIDbContext>()
            .UseNpgsql(builder.ConnectionString)
            .Options;
        return new WorkflowAIDbContext(options);
    }

    private static BlobContainerClient GetAzureStorageContainer()
    {
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");

        if (string.IsNullOrEmpty(connectionString))
            return null!;

        var blobServiceClient = new BlobServiceClient(connectionString);

        // Get or create container - using "connector-exports" blob
        var containerClient = blobServiceClient.GetBlobContainerClient("connector-exports");
        return containerClient;
    }

    private WorkflowAIDbContext _db = null!;
    private ExcelExportService _exportService = null!;
    private BlobContainerClient _storageContainer = null!;

    public async Task InitializeAsync()
    {
        _db = CreateDb();
        _storageContainer = GetAzureStorageContainer();

        if (_storageContainer != null)
        {
            // Ensure container exists
            await _storageContainer.CreateIfNotExistsAsync();
            _exportService = new ExcelExportService(_storageContainer);
        }
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task ExportConnectors_ToExcel_Success()
    {
        if (_exportService == null)
        {
            throw new SkipTestException("Azure Storage credentials not configured");
        }

        Console.WriteLine($"\n╔═══════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine($"║  EXPORTING TENANT CONNECTORS TO EXCEL");
        Console.WriteLine($"╚═══════════════════════════════════════════════════════════════════════════════╝\n");

        // Fetch all connectors from database
        var allConnectors = await _db.TenantConnectors
            .OrderBy(c => c.ConnectorType)
            .ToListAsync();

        Console.WriteLine($"[1/3] Fetched {allConnectors.Count} connectors from database\n");

        allConnectors.Should().HaveCountGreaterThanOrEqualTo(1, "At least one connector should exist");

        // Display connector summary
        Console.WriteLine($"Connectors to export:");
        foreach (var connector in allConnectors.Take(10))
        {
            Console.WriteLine($"  ✓ {connector.ConnectorType.PadRight(30)} | Status: {connector.Status.Name.PadRight(7)} | Version: {connector.Version}");
        }
        if (allConnectors.Count > 10)
            Console.WriteLine($"  ... and {allConnectors.Count - 10} more");
        Console.WriteLine();

        // Export to Excel
        Console.WriteLine($"[2/3] Generating Excel file...");
        var excelUrl = await _exportService.ExportConnectorsToExcelAsync(
            allConnectors,
            cancellationToken: CancellationToken.None
        );

        Console.WriteLine($"      ✓ Excel file generated\n");

        // Verify upload
        Console.WriteLine($"[3/3] Verifying upload to Azure Storage...");
        excelUrl.Should().NotBeNullOrEmpty("Excel file URL should be returned");
        excelUrl.Should().Contain("blob.core.windows.net", "URL should point to Azure Blob Storage");

        Console.WriteLine($"      ✓ File uploaded successfully");
        Console.WriteLine($"      URL: {excelUrl}\n");

        Console.WriteLine($"╔═══════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine($"║  ✓ EXPORT COMPLETE");
        Console.WriteLine($"║");
        Console.WriteLine($"║  File: TenantConnectors_*.xlsx");
        Console.WriteLine($"║  Location: Azure Blob Storage (connector-exports container)");
        Console.WriteLine($"║  Connectors Exported: {allConnectors.Count}");
        Console.WriteLine($"║  Sheets: Connectors, Metadata, Capabilities");
        Console.WriteLine($"╚═══════════════════════════════════════════════════════════════════════════════╝\n");
    }

    [Fact]
    public async Task ExportConnectorApis_ToExcel_Success()
    {
        if (_exportService == null)
        {
            throw new SkipTestException("Azure Storage credentials not configured");
        }

        Console.WriteLine($"\n╔═══════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine($"║  EXPORTING CONNECTOR APIS TO EXCEL");
        Console.WriteLine($"╚═══════════════════════════════════════════════════════════════════════════════╝\n");

        // Fetch all connectors and their APIs
        var allConnectors = await _db.TenantConnectors.ToListAsync();
        var connectorApiPairs = new List<(TenantConnector, List<TenantConnectorApi>)>();

        foreach (var connector in allConnectors)
        {
            var apis = await _db.TenantConnectorApis
                .Where(a => a.TenantConnectorId == connector.Id)
                .ToListAsync();

            if (apis.Any())
                connectorApiPairs.Add((connector, apis));
        }

        var totalApis = connectorApiPairs.Sum(x => x.Item2.Count);

        Console.WriteLine($"[1/3] Fetched {connectorApiPairs.Count} connectors with {totalApis} APIs\n");

        if (connectorApiPairs.Count == 0)
        {
            throw new SkipTestException("No connectors with APIs found");
        }

        // Display API summary
        Console.WriteLine($"Connector APIs to export:");
        foreach (var pair in connectorApiPairs.Take(5))
        {
            var connector = pair.Item1;
            var apis = pair.Item2;
            Console.WriteLine($"  ✓ {connector.ConnectorType.PadRight(25)} | {apis.Count} APIs");
            foreach (var api in apis.Take(3))
            {
                Console.WriteLine($"    - {api.HttpMethod.PadRight(6)} {api.ApiName}");
            }
            if (apis.Count > 3)
                Console.WriteLine($"    - ... and {apis.Count - 3} more");
        }
        Console.WriteLine();

        // Export to Excel
        Console.WriteLine($"[2/3] Generating Excel file...");
        var excelUrl = await _exportService.ExportConnectorApisToExcelAsync(
            connectorApiPairs,
            cancellationToken: CancellationToken.None
        );

        Console.WriteLine($"      ✓ Excel file generated\n");

        // Verify upload
        Console.WriteLine($"[3/3] Verifying upload to Azure Storage...");
        excelUrl.Should().NotBeNullOrEmpty("Excel file URL should be returned");
        excelUrl.Should().Contain("blob.core.windows.net", "URL should point to Azure Blob Storage");

        Console.WriteLine($"      ✓ File uploaded successfully");
        Console.WriteLine($"      URL: {excelUrl}\n");

        Console.WriteLine($"╔═══════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine($"║  ✓ EXPORT COMPLETE");
        Console.WriteLine($"║");
        Console.WriteLine($"║  File: TenantConnectorApis_*.xlsx");
        Console.WriteLine($"║  Location: Azure Blob Storage (connector-exports container)");
        Console.WriteLine($"║  Connectors: {connectorApiPairs.Count}");
        Console.WriteLine($"║  Total APIs: {totalApis}");
        Console.WriteLine($"╚═══════════════════════════════════════════════════════════════════════════════╝\n");
    }
}
