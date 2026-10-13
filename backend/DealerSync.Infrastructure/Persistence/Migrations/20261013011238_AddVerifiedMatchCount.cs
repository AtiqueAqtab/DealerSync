using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealerSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVerifiedMatchCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VerifiedMatches",
                table: "ReconciliationRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VerifiedMatches",
                table: "ReconciliationRuns");
        }
    }
}
