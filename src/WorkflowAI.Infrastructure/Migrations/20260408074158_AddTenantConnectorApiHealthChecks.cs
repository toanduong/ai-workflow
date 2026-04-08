using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantConnectorApiHealthChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantConnectorApiHealthChecks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantConnectorApiId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantConnectorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApiName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    HttpMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ResolvedUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantConnectorApiHealthChecks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantConnectorApiHealthChecks_TenantConnectorApiId",
                table: "TenantConnectorApiHealthChecks",
                column: "TenantConnectorApiId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantConnectorApiHealthChecks_TenantConnectorId",
                table: "TenantConnectorApiHealthChecks",
                column: "TenantConnectorId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantConnectorApiHealthChecks_TenantConnectorId_CheckedAt",
                table: "TenantConnectorApiHealthChecks",
                columns: new[] { "TenantConnectorId", "CheckedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantConnectorApiHealthChecks");
        }
    }
}
