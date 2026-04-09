using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAIModels_AddAIModelTypeToTenantConnectors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIModels");

            migrationBuilder.AddColumn<string>(
                name: "AIModelType",
                table: "TenantConnectors",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AIModelType",
                table: "TenantConnectors");

            migrationBuilder.CreateTable(
                name: "AIModels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextWindow = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    InputCostPer1KTokens = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ModelId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OutputCostPer1KTokens = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIModels", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIModels_ModelId",
                table: "AIModels",
                column: "ModelId",
                unique: true);
        }
    }
}
