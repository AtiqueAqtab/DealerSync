using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealerSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultipleSuppliersPerShopifySku : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SkuMappings_ShopifySku",
                table: "SkuMappings");

            migrationBuilder.CreateIndex(
                name: "IX_SkuMappings_ShopifySku",
                table: "SkuMappings",
                column: "ShopifySku");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SkuMappings_ShopifySku",
                table: "SkuMappings");

            migrationBuilder.CreateIndex(
                name: "IX_SkuMappings_ShopifySku",
                table: "SkuMappings",
                column: "ShopifySku",
                unique: true);
        }
    }
}
