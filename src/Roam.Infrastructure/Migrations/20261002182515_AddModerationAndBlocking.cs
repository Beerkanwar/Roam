using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddModerationAndBlocking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModerationCases",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ReportId = table.Column<string>(type: "text", nullable: true),
                    TargetUserId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModerationCases_AspNetUsers_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModerationCases_Reports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "Reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserBlocks",
                columns: table => new
                {
                    BlockerId = table.Column<string>(type: "text", nullable: false),
                    BlockedId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBlocks", x => new { x.BlockerId, x.BlockedId });
                    table.ForeignKey(
                        name: "FK_UserBlocks_AspNetUsers_BlockedId",
                        column: x => x.BlockedId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserBlocks_AspNetUsers_BlockerId",
                        column: x => x.BlockerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModerationAuditLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ModerationCaseId = table.Column<string>(type: "text", nullable: false),
                    TargetUserId = table.Column<string>(type: "text", nullable: false),
                    ModeratorId = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModerationAuditLogs_AspNetUsers_ModeratorId",
                        column: x => x.ModeratorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ModerationAuditLogs_AspNetUsers_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModerationAuditLogs_ModerationCases_ModerationCaseId",
                        column: x => x.ModerationCaseId,
                        principalTable: "ModerationCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationAuditLogs_ModerationCaseId",
                table: "ModerationAuditLogs",
                column: "ModerationCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationAuditLogs_ModeratorId",
                table: "ModerationAuditLogs",
                column: "ModeratorId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationAuditLogs_TargetUserId",
                table: "ModerationAuditLogs",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationCases_ReportId",
                table: "ModerationCases",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationCases_Status",
                table: "ModerationCases",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationCases_TargetUserId",
                table: "ModerationCases",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBlocks_BlockedId",
                table: "UserBlocks",
                column: "BlockedId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModerationAuditLogs");

            migrationBuilder.DropTable(
                name: "UserBlocks");

            migrationBuilder.DropTable(
                name: "ModerationCases");
        }
    }
}
