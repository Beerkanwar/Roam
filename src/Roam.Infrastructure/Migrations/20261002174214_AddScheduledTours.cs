using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledTours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedVolunteerId",
                table: "TourRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "TourRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "TourRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelledByUserId",
                table: "TourRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WasLateCancellation",
                table: "TourRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ScheduledTours",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TourRequestId = table.Column<string>(type: "text", nullable: false),
                    ProposedByUserId = table.Column<string>(type: "text", nullable: false),
                    ProposedStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProposedEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequesterAcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VolunteerAcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledByUserId = table.Column<string>(type: "text", nullable: true),
                    CancellationReason = table.Column<string>(type: "text", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledTours", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduledTours");

            migrationBuilder.DropColumn(
                name: "AssignedVolunteerId",
                table: "TourRequests");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "TourRequests");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "TourRequests");

            migrationBuilder.DropColumn(
                name: "CancelledByUserId",
                table: "TourRequests");

            migrationBuilder.DropColumn(
                name: "WasLateCancellation",
                table: "TourRequests");
        }
    }
}
