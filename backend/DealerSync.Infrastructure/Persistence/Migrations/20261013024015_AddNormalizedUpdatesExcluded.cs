using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealerSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNormalizedUpdatesExcluded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NormalizedUpdatesExcluded",
                table: "ReconciliationRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NormalizedUpdatesExcluded",
                table: "ReconciliationRuns");
        }
    }
}
