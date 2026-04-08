using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameConnectorNameToConnectorTypeAndAddVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename ConnectorName to ConnectorType on TenantConnectors
            migrationBuilder.RenameColumn(
                name: "ConnectorName",
                table: "TenantConnectors",
                newName: "ConnectorType");

            // Rename ConnectorName to ConnectorType on TenantConnectorApis
            migrationBuilder.RenameColumn(
                name: "ConnectorName",
                table: "TenantConnectorApis",
                newName: "ConnectorType");

            // Rename the unique index on TenantConnectors
            migrationBuilder.RenameIndex(
                name: "IX_TenantConnectors_TenantId_ConnectorName",
                table: "TenantConnectors",
                newName: "IX_TenantConnectors_TenantId_ConnectorType");

            // Add Version column to TenantConnectors
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "TenantConnectors",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop Version column
            migrationBuilder.DropColumn(
                name: "Version",
                table: "TenantConnectors");

            // Rename ConnectorType back to ConnectorName on TenantConnectors
            migrationBuilder.RenameColumn(
                name: "ConnectorType",
                table: "TenantConnectors",
                newName: "ConnectorName");

            // Rename ConnectorType back to ConnectorName on TenantConnectorApis
            migrationBuilder.RenameColumn(
                name: "ConnectorType",
                table: "TenantConnectorApis",
                newName: "ConnectorName");

            // Rename the unique index back
            migrationBuilder.RenameIndex(
                name: "IX_TenantConnectors_TenantId_ConnectorType",
                table: "TenantConnectors",
                newName: "IX_TenantConnectors_TenantId_ConnectorName");
        }
    }
}
