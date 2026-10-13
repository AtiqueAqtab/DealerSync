using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DealerSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUniqueSkuMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SkuMappings_LightspeedPartNumber",
                table: "SkuMappings");

            migrationBuilder.DropIndex(
                name: "IX_SkuMappings_ShopifySku",
                table: "SkuMappings");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.AlterColumn<string>(
                name: "ShopifySku",
                table: "SkuMappings",
                type: "citext",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "LightspeedPartNumber",
                table: "SkuMappings",
                type: "citext",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.CreateIndex(
                name: "IX_SkuMappings_LightspeedPartNumber",
                table: "SkuMappings",
                column: "LightspeedPartNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SkuMappings_ShopifySku",
                table: "SkuMappings",
                column: "ShopifySku",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SkuMappings_LightspeedPartNumber",
                table: "SkuMappings");

            migrationBuilder.DropIndex(
                name: "IX_SkuMappings_ShopifySku",
                table: "SkuMappings");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.AlterColumn<string>(
                name: "ShopifySku",
                table: "SkuMappings",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "citext",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "LightspeedPartNumber",
                table: "SkuMappings",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "citext",
                oldMaxLength: 255);

            migrationBuilder.CreateIndex(
                name: "IX_SkuMappings_LightspeedPartNumber",
                table: "SkuMappings",
                column: "LightspeedPartNumber");

            migrationBuilder.CreateIndex(
                name: "IX_SkuMappings_ShopifySku",
                table: "SkuMappings",
                column: "ShopifySku");
        }
    }
}
