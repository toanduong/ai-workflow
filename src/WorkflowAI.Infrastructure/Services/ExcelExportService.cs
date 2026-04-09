using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using OfficeOpenXml;
using WorkflowAI.Domain.TenantConnectors;

namespace WorkflowAI.Infrastructure.Services;

/// <summary>
/// Service for exporting TenantConnectors data to Excel and uploading to Azure Blob Storage.
/// </summary>
public sealed class ExcelExportService
{
    private readonly BlobContainerClient _containerClient;

    public ExcelExportService(BlobContainerClient containerClient)
    {
        _containerClient = containerClient ?? throw new ArgumentNullException(nameof(containerClient));
        // Set EPPlus license context for non-commercial use
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    /// <summary>
    /// Export TenantConnectors to Excel and upload to Azure Blob Storage.
    /// </summary>
    public async Task<string?> ExportConnectorsToExcelAsync(
        IEnumerable<TenantConnector> connectors,
        string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        fileName ??= $"TenantConnectors_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

        using (var package = new ExcelPackage())
        {
            // Create worksheets
            CreateConnectorsSheet(package, connectors);
            CreateMetadataSheet(package, connectors);
            CreateCapabilitiesSheet(package, connectors);

            // Export to byte array
            var excelBytes = package.GetAsByteArray();

            // Upload to Azure Blob Storage
            var blobClient = _containerClient.GetBlobClient(fileName);
            var httpHeaders = new BlobHttpHeaders
            {
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            };

            await blobClient.UploadAsync(
                BinaryData.FromBytes(excelBytes),
                overwrite: true,
                cancellationToken: cancellationToken);

            // Set headers
            await blobClient.SetHttpHeadersAsync(httpHeaders, cancellationToken: cancellationToken);

            return blobClient.Uri.ToString();
        }
    }

    /// <summary>
    /// Export TenantConnectorApis to Excel and upload to Azure Blob Storage.
    /// </summary>
    public async Task<string?> ExportConnectorApisToExcelAsync(
        IEnumerable<(TenantConnector Connector, List<TenantConnectorApi> Apis)> connectorApiPairs,
        string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        fileName ??= $"TenantConnectorApis_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

        using (var package = new ExcelPackage())
        {
            // Create APIs sheet
            var worksheet = package.Workbook.Worksheets.Add("APIs");
            int row = 1;

            // Headers
            worksheet.Cells[row, 1].Value = "Connector Type";
            worksheet.Cells[row, 2].Value = "API Name";
            worksheet.Cells[row, 3].Value = "HTTP Method";
            worksheet.Cells[row, 4].Value = "URL Template";
            worksheet.Cells[row, 5].Value = "Metadata";

            // Style header
            for (int col = 1; col <= 5; col++)
            {
                var cell = worksheet.Cells[row, col];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            }

            row++;

            // Data rows
            foreach (var (connector, apis) in connectorApiPairs)
            {
                foreach (var api in apis)
                {
                    worksheet.Cells[row, 1].Value = connector.ConnectorType;
                    worksheet.Cells[row, 2].Value = api.ApiName;
                    worksheet.Cells[row, 3].Value = api.HttpMethod;
                    worksheet.Cells[row, 4].Value = api.UrlTemplate;
                    worksheet.Cells[row, 5].Value = api.Metadata;
                    row++;
                }
            }

            // Auto-fit columns
            worksheet.Column(1).Width = 25;
            worksheet.Column(2).Width = 30;
            worksheet.Column(3).Width = 15;
            worksheet.Column(4).Width = 50;
            worksheet.Column(5).Width = 30;

            // Export to byte array
            var excelBytes = package.GetAsByteArray();

            // Upload to Azure Blob Storage
            var blobClient = _containerClient.GetBlobClient(fileName);
            var httpHeaders = new BlobHttpHeaders
            {
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            };

            await blobClient.UploadAsync(
                BinaryData.FromBytes(excelBytes),
                overwrite: true,
                cancellationToken: cancellationToken);

            // Set headers
            await blobClient.SetHttpHeadersAsync(httpHeaders, cancellationToken: cancellationToken);

            return blobClient.Uri.ToString();
        }
    }

    private static void CreateConnectorsSheet(ExcelPackage package, IEnumerable<TenantConnector> connectors)
    {
        var worksheet = package.Workbook.Worksheets.Add("Connectors");
        int row = 1;

        // Headers
        worksheet.Cells[row, 1].Value = "ID";
        worksheet.Cells[row, 2].Value = "Tenant ID";
        worksheet.Cells[row, 3].Value = "Connector Type";
        worksheet.Cells[row, 4].Value = "Status";
        worksheet.Cells[row, 5].Value = "Version";
        worksheet.Cells[row, 6].Value = "Created At";
        worksheet.Cells[row, 7].Value = "Updated At";

        // Style header
        for (int col = 1; col <= 7; col++)
        {
            var cell = worksheet.Cells[row, col];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
        }

        row++;

        // Data rows
        foreach (var connector in connectors)
        {
            worksheet.Cells[row, 1].Value = connector.Id.Value.ToString();
            worksheet.Cells[row, 2].Value = connector.TenantId.Value.ToString();
            worksheet.Cells[row, 3].Value = connector.ConnectorType;
            worksheet.Cells[row, 4].Value = connector.Status.Name;
            worksheet.Cells[row, 5].Value = connector.Version;
            worksheet.Cells[row, 6].Value = connector.CreatedAt;
            worksheet.Cells[row, 7].Value = connector.UpdatedAt;

            // Format dates
            worksheet.Cells[row, 6].Style.Numberformat.Format = "yyyy-mm-dd hh:mm:ss";
            worksheet.Cells[row, 7].Style.Numberformat.Format = "yyyy-mm-dd hh:mm:ss";

            row++;
        }

        // Auto-fit columns
        for (int col = 1; col <= 7; col++)
            worksheet.Column(col).AutoFit();
    }

    private static void CreateMetadataSheet(ExcelPackage package, IEnumerable<TenantConnector> connectors)
    {
        var worksheet = package.Workbook.Worksheets.Add("Metadata");
        int row = 1;

        // Headers
        worksheet.Cells[row, 1].Value = "Connector Type";
        worksheet.Cells[row, 2].Value = "Auth Type";
        worksheet.Cells[row, 3].Value = "Required Fields";
        worksheet.Cells[row, 4].Value = "Base URL";

        // Style header
        for (int col = 1; col <= 4; col++)
        {
            var cell = worksheet.Cells[row, col];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
        }

        row++;

        // Data rows
        foreach (var connector in connectors)
        {
            try
            {
                var doc = JsonDocument.Parse(connector.Metadata);
                var root = doc.RootElement;

                var authType = root.TryGetProperty("authType", out var authEl) ? authEl.GetString() : "N/A";
                var requiredFields = string.Empty;
                if (root.TryGetProperty("requiredFields", out var fieldsEl) && fieldsEl.ValueKind == JsonValueKind.Array)
                {
                    requiredFields = string.Join(", ", fieldsEl.EnumerateArray().Select(e => e.GetString()));
                }
                var baseUrl = root.TryGetProperty("baseUrl", out var urlEl) ? urlEl.GetString() : "N/A";

                worksheet.Cells[row, 1].Value = connector.ConnectorType;
                worksheet.Cells[row, 2].Value = authType;
                worksheet.Cells[row, 3].Value = requiredFields;
                worksheet.Cells[row, 4].Value = baseUrl;

                row++;
            }
            catch
            {
                // Skip if metadata is invalid JSON
            }
        }

        // Auto-fit columns
        for (int col = 1; col <= 4; col++)
            worksheet.Column(col).AutoFit();
    }

    private static void CreateCapabilitiesSheet(ExcelPackage package, IEnumerable<TenantConnector> connectors)
    {
        var worksheet = package.Workbook.Worksheets.Add("Capabilities");
        int row = 1;

        // Headers
        worksheet.Cells[row, 1].Value = "Connector Type";
        worksheet.Cells[row, 2].Value = "Description";
        worksheet.Cells[row, 3].Value = "Capabilities";
        worksheet.Cells[row, 4].Value = "Rate Limits";
        worksheet.Cells[row, 5].Value = "Webhook Support";

        // Style header
        for (int col = 1; col <= 5; col++)
        {
            var cell = worksheet.Cells[row, col];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightCoral);
            cell.Style.WrapText = true;
        }

        row++;

        // Data rows
        foreach (var connector in connectors)
        {
            try
            {
                var doc = JsonDocument.Parse(connector.Info);
                var root = doc.RootElement;

                var description = root.TryGetProperty("description", out var descEl) ? descEl.GetString() : "N/A";
                var capabilities = string.Empty;
                if (root.TryGetProperty("capabilities", out var capEl) && capEl.ValueKind == JsonValueKind.Array)
                {
                    capabilities = string.Join(", ", capEl.EnumerateArray().Select(e => e.GetString()));
                }
                var rateLimits = root.TryGetProperty("rateLimits", out var rateEl) ? rateEl.GetString() : "N/A";
                var webhookSupport = root.TryGetProperty("webhookSupport", out var webhookEl) ? webhookEl.GetBoolean().ToString() : "N/A";

                worksheet.Cells[row, 1].Value = connector.ConnectorType;
                worksheet.Cells[row, 2].Value = description;
                worksheet.Cells[row, 3].Value = capabilities;
                worksheet.Cells[row, 4].Value = rateLimits;
                worksheet.Cells[row, 5].Value = webhookSupport;

                worksheet.Cells[row, 2].Style.WrapText = true;
                worksheet.Cells[row, 3].Style.WrapText = true;

                row++;
            }
            catch
            {
                // Skip if info is invalid JSON
            }
        }

        // Auto-fit columns
        worksheet.Column(1).Width = 25;
        worksheet.Column(2).Width = 40;
        worksheet.Column(3).Width = 50;
        worksheet.Column(4).Width = 25;
        worksheet.Column(5).Width = 15;
    }
}
