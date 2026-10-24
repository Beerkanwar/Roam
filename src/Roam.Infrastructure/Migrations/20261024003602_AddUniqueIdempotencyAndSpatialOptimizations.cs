using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIdempotencyAndSpatialOptimizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TipTransactions_SenderId_IdempotencyKey",
                table: "TipTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Places_Name",
                table: "Places");

            migrationBuilder.DropIndex(
                name: "IX_Donations_DonorId_IdempotencyKey",
                table: "Donations");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateIndex(
                name: "IX_TipTransactions_SenderId_IdempotencyKey",
                table: "TipTransactions",
                columns: new[] { "SenderId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Places_Name",
                table: "Places",
                column: "Name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Donations_DonorId_IdempotencyKey",
                table: "Donations",
                columns: new[] { "DonorId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TipTransactions_SenderId_IdempotencyKey",
                table: "TipTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Places_Name",
                table: "Places");

            migrationBuilder.DropIndex(
                name: "IX_Donations_DonorId_IdempotencyKey",
                table: "Donations");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateIndex(
                name: "IX_TipTransactions_SenderId_IdempotencyKey",
                table: "TipTransactions",
                columns: new[] { "SenderId", "IdempotencyKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Places_Name",
                table: "Places",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Donations_DonorId_IdempotencyKey",
                table: "Donations",
                columns: new[] { "DonorId", "IdempotencyKey" });
        }
    }
}
