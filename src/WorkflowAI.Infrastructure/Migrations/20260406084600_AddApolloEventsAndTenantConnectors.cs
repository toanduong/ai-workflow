using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApolloEventsAndTenantConnectors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApolloEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    ClaudeResponse = table.Column<string>(type: "text", nullable: true),
                    WorkflowExecutionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApolloEvents", x => x.Id);
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
                name: "IX_ApolloEvents_CreatedAt",
                table: "ApolloEvents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ApolloEvents_EventType",
                table: "ApolloEvents",
                column: "EventType");

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
                name: "ApolloEvents");

            migrationBuilder.DropTable(
                name: "TenantConnectors");
        }
    }
}
