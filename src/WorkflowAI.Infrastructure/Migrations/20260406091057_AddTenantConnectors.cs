using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantConnectors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantConnectorApis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantConnectorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApiName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    HttpMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    UrlTemplate = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantConnectorApis", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantConnectors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: false),
                    Info = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantConnectors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantConnectorApis_TenantConnectorId",
                table: "TenantConnectorApis",
                column: "TenantConnectorId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantConnectorApis_TenantConnectorId_ApiName",
                table: "TenantConnectorApis",
                columns: new[] { "TenantConnectorId", "ApiName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantConnectors_TenantId",
                table: "TenantConnectors",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantConnectors_TenantId_ConnectorName",
                table: "TenantConnectors",
                columns: new[] { "TenantId", "ConnectorName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantConnectorApis");

            migrationBuilder.DropTable(
                name: "TenantConnectors");
        }
    }
}
