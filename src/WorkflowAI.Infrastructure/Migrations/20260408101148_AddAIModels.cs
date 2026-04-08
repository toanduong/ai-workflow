using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAIModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AIModels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    InputCostPer1KTokens = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    OutputCostPer1KTokens = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ContextWindow = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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

            // Seed initial AI models
            var now = new DateTime(2026, 4, 8, 0, 0, 0, DateTimeKind.Utc);
            migrationBuilder.InsertData(
                table: "AIModels",
                columns: ["Id", "ModelId", "Provider", "DisplayName", "InputCostPer1KTokens", "OutputCostPer1KTokens", "ContextWindow", "IsEnabled", "IsDefault", "Notes", "CreatedAt"],
                values: new object[,]
                {
                    { Guid.NewGuid(), "claude-sonnet-4-6",  "Anthropic", "Claude Sonnet 4.6",  0.003m,    0.015m,    200000,  true,  true,  "Recommended default — best balance of speed, cost, and capability.", now },
                    { Guid.NewGuid(), "claude-opus-4-6",    "Anthropic", "Claude Opus 4.6",    0.015m,    0.075m,    200000,  true,  false, "Highest capability Anthropic model. Use for complex reasoning tasks.", now },
                    { Guid.NewGuid(), "claude-haiku-4-5",   "Anthropic", "Claude Haiku 4.5",   0.00025m,  0.00125m,  200000,  true,  false, "Fastest and most cost-efficient Anthropic model.", now },
                    { Guid.NewGuid(), "gpt-4o",             "OpenAI",    "GPT-4o",             0.005m,    0.015m,    128000,  true,  false, "OpenAI flagship multimodal model.", now },
                    { Guid.NewGuid(), "gpt-4o-mini",        "OpenAI",    "GPT-4o Mini",        0.00015m,  0.0006m,   128000,  true,  false, "Cost-efficient OpenAI model for lighter tasks.", now },
                    { Guid.NewGuid(), "gemini-1.5-pro",     "Google",    "Gemini 1.5 Pro",     0.0035m,   0.0105m,   1000000, true,  false, "Google flagship model with 1M token context window.", now },
                    { Guid.NewGuid(), "gemini-1.5-flash",   "Google",    "Gemini 1.5 Flash",   0.000075m, 0.0003m,   1000000, true,  false, "Fast and affordable Google model with 1M token context.", now },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIModels");
        }
    }
}
