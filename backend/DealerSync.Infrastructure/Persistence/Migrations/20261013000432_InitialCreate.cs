using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealerSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReconciliationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LightspeedItemCount = table.Column<int>(type: "integer", nullable: false),
                    ShopifyItemCount = table.Column<int>(type: "integer", nullable: false),
                    ExactMatches = table.Column<int>(type: "integer", nullable: false),
                    NormalizedMatches = table.Column<int>(type: "integer", nullable: false),
                    Unmatched = table.Column<int>(type: "integer", nullable: false),
                    InventoryDifferences = table.Column<int>(type: "integer", nullable: false),
                    InventoryUpdatesGenerated = table.Column<int>(type: "integer", nullable: false),
                    NegativeQuantitiesExcluded = table.Column<int>(type: "integer", nullable: false),
                    ShopifyCoverage = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SkuMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LightspeedPartNumber = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ShopifySku = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkuMappings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SkuMappings_LightspeedPartNumber",
                table: "SkuMappings",
                column: "LightspeedPartNumber");

            migrationBuilder.CreateIndex(
                name: "IX_SkuMappings_ShopifySku",
                table: "SkuMappings",
                column: "ShopifySku");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReconciliationRuns");

            migrationBuilder.DropTable(
                name: "SkuMappings");
        }
    }
}
