using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexingAndConstraintOptimizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TipTransactions_IdempotencyKey",
                table: "TipTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Donations_DonorId",
                table: "Donations");

            migrationBuilder.DropIndex(
                name: "IX_Donations_IdempotencyKey",
                table: "Donations");

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

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SessionId_SentAt",
                table: "ChatMessages",
                columns: new[] { "SessionId", "SentAt" });
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

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_SessionId_SentAt",
                table: "ChatMessages");

            migrationBuilder.CreateIndex(
                name: "IX_TipTransactions_IdempotencyKey",
                table: "TipTransactions",
                column: "IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_Donations_DonorId",
                table: "Donations",
                column: "DonorId");

            migrationBuilder.CreateIndex(
                name: "IX_Donations_IdempotencyKey",
                table: "Donations",
                column: "IdempotencyKey");
        }
    }
}
