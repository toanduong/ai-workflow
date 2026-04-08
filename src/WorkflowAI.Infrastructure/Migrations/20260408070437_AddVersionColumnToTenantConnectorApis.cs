using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVersionColumnToTenantConnectorApis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "TenantConnectorApis",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "TenantConnectorApis");
        }
    }
}
