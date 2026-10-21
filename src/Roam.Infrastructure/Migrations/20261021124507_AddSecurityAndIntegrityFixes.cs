using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityAndIntegrityFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_AspNetUsers_SenderId",
                table: "ChatMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_ModerationAuditLogs_AspNetUsers_ModeratorId",
                table: "ModerationAuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_TourRequests_Places_PlaceId",
                table: "TourRequests");

            migrationBuilder.DropIndex(
                name: "IX_TourParticipants_TourSessionId",
                table: "TourParticipants");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "TourRequests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TourSessionId",
                table: "TipTransactions",
                type: "text",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SenderId",
                table: "TipTransactions",
                type: "text",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "ReceiverId",
                table: "TipTransactions",
                type: "text",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "TipTransactions",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "TipTransactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "TipTransactions",
                type: "text",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "TipTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Reports",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TourSessionId",
                table: "Feedbacks",
                type: "text",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "TargetUserId",
                table: "Feedbacks",
                type: "text",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "ReviewerId",
                table: "Feedbacks",
                type: "text",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "Comments",
                table: "Feedbacks",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "Feedbacks",
                type: "text",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "DonorId",
                table: "Donations",
                type: "text",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "Donations",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Donations",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "Donations",
                type: "text",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Donations",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLocations_UserId",
                table: "UserLocations",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TourSessions_HostVolunteerId",
                table: "TourSessions",
                column: "HostVolunteerId");

            migrationBuilder.CreateIndex(
                name: "IX_TourSessions_StartedAt",
                table: "TourSessions",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TourSessions_Status",
                table: "TourSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TourSessions_TourRequestId",
                table: "TourSessions",
                column: "TourRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_TourRequests_AssignedVolunteerId",
                table: "TourRequests",
                column: "AssignedVolunteerId");

            migrationBuilder.CreateIndex(
                name: "IX_TourRequests_CreatedAt",
                table: "TourRequests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TourRequests_RequesterId",
                table: "TourRequests",
                column: "RequesterId");

            migrationBuilder.CreateIndex(
                name: "IX_TourRequests_Status_ExpiresAt",
                table: "TourRequests",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TourParticipants_TourSessionId_UserId",
                table: "TourParticipants",
                columns: new[] { "TourSessionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TourParticipants_UserId",
                table: "TourParticipants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TipTransactions_CreatedAt",
                table: "TipTransactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TipTransactions_IdempotencyKey",
                table: "TipTransactions",
                column: "IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_TipTransactions_ReceiverId",
                table: "TipTransactions",
                column: "ReceiverId");

            migrationBuilder.CreateIndex(
                name: "IX_TipTransactions_SenderId",
                table: "TipTransactions",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_TipTransactions_TourSessionId",
                table: "TipTransactions",
                column: "TourSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTours_ProposedByUserId",
                table: "ScheduledTours",
                column: "ProposedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTours_ProposedStartUtc",
                table: "ScheduledTours",
                column: "ProposedStartUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTours_Status",
                table: "ScheduledTours",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTours_TourRequestId",
                table: "ScheduledTours",
                column: "TourRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_CreatedAt",
                table: "Reports",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReportedUserId",
                table: "Reports",
                column: "ReportedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReporterId",
                table: "Reports",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_Status",
                table: "Reports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_TourSessionId",
                table: "Reports",
                column: "TourSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReliabilityEvents_CreatedAt",
                table: "ReliabilityEvents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReliabilityEvents_RelatedSessionId",
                table: "ReliabilityEvents",
                column: "RelatedSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReliabilityEvents_Type",
                table: "ReliabilityEvents",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_ReliabilityEvents_UserId",
                table: "ReliabilityEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_CreatedAt",
                table: "Feedbacks",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_ReviewerId",
                table: "Feedbacks",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_TargetUserId",
                table: "Feedbacks",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_TourSessionId_ReviewerId_TargetUserId",
                table: "Feedbacks",
                columns: new[] { "TourSessionId", "ReviewerId", "TargetUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Donations_CreatedAt",
                table: "Donations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Donations_DonorId",
                table: "Donations",
                column: "DonorId");

            migrationBuilder.CreateIndex(
                name: "IX_Donations_IdempotencyKey",
                table: "Donations",
                column: "IdempotencyKey");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_AspNetUsers_SenderId",
                table: "ChatMessages",
                column: "SenderId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Donations_AspNetUsers_DonorId",
                table: "Donations",
                column: "DonorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_AspNetUsers_ReviewerId",
                table: "Feedbacks",
                column: "ReviewerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_AspNetUsers_TargetUserId",
                table: "Feedbacks",
                column: "TargetUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_TourSessions_TourSessionId",
                table: "Feedbacks",
                column: "TourSessionId",
                principalTable: "TourSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ModerationAuditLogs_AspNetUsers_ModeratorId",
                table: "ModerationAuditLogs",
                column: "ModeratorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReliabilityEvents_AspNetUsers_UserId",
                table: "ReliabilityEvents",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_AspNetUsers_ReportedUserId",
                table: "Reports",
                column: "ReportedUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_AspNetUsers_ReporterId",
                table: "Reports",
                column: "ReporterId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduledTours_AspNetUsers_ProposedByUserId",
                table: "ScheduledTours",
                column: "ProposedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduledTours_TourRequests_TourRequestId",
                table: "ScheduledTours",
                column: "TourRequestId",
                principalTable: "TourRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TipTransactions_AspNetUsers_ReceiverId",
                table: "TipTransactions",
                column: "ReceiverId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TipTransactions_AspNetUsers_SenderId",
                table: "TipTransactions",
                column: "SenderId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TipTransactions_TourSessions_TourSessionId",
                table: "TipTransactions",
                column: "TourSessionId",
                principalTable: "TourSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TourParticipants_AspNetUsers_UserId",
                table: "TourParticipants",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TourRequests_AspNetUsers_AssignedVolunteerId",
                table: "TourRequests",
                column: "AssignedVolunteerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TourRequests_AspNetUsers_RequesterId",
                table: "TourRequests",
                column: "RequesterId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TourRequests_Places_PlaceId",
                table: "TourRequests",
                column: "PlaceId",
                principalTable: "Places",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TourSessions_AspNetUsers_HostVolunteerId",
                table: "TourSessions",
                column: "HostVolunteerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TourSessions_TourRequests_TourRequestId",
                table: "TourSessions",
                column: "TourRequestId",
                principalTable: "TourRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserLocations_AspNetUsers_UserId",
                table: "UserLocations",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_AspNetUsers_SenderId",
                table: "ChatMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_Donations_AspNetUsers_DonorId",
                table: "Donations");

            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_AspNetUsers_ReviewerId",
                table: "Feedbacks");

            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_AspNetUsers_TargetUserId",
                table: "Feedbacks");

            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_TourSessions_TourSessionId",
                table: "Feedbacks");

            migrationBuilder.DropForeignKey(
                name: "FK_ModerationAuditLogs_AspNetUsers_ModeratorId",
                table: "ModerationAuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_ReliabilityEvents_AspNetUsers_UserId",
                table: "ReliabilityEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_Reports_AspNetUsers_ReportedUserId",
                table: "Reports");

            migrationBuilder.DropForeignKey(
                name: "FK_Reports_AspNetUsers_ReporterId",
                table: "Reports");

            migrationBuilder.DropForeignKey(
                name: "FK_ScheduledTours_AspNetUsers_ProposedByUserId",
                table: "ScheduledTours");

            migrationBuilder.DropForeignKey(
                name: "FK_ScheduledTours_TourRequests_TourRequestId",
                table: "ScheduledTours");

            migrationBuilder.DropForeignKey(
                name: "FK_TipTransactions_AspNetUsers_ReceiverId",
                table: "TipTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_TipTransactions_AspNetUsers_SenderId",
                table: "TipTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_TipTransactions_TourSessions_TourSessionId",
                table: "TipTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_TourParticipants_AspNetUsers_UserId",
                table: "TourParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_TourRequests_AspNetUsers_AssignedVolunteerId",
                table: "TourRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_TourRequests_AspNetUsers_RequesterId",
                table: "TourRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_TourRequests_Places_PlaceId",
                table: "TourRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_TourSessions_AspNetUsers_HostVolunteerId",
                table: "TourSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_TourSessions_TourRequests_TourRequestId",
                table: "TourSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserLocations_AspNetUsers_UserId",
                table: "UserLocations");

            migrationBuilder.DropIndex(
                name: "IX_UserLocations_UserId",
                table: "UserLocations");

            migrationBuilder.DropIndex(
                name: "IX_TourSessions_HostVolunteerId",
                table: "TourSessions");

            migrationBuilder.DropIndex(
                name: "IX_TourSessions_StartedAt",
                table: "TourSessions");

            migrationBuilder.DropIndex(
                name: "IX_TourSessions_Status",
                table: "TourSessions");

            migrationBuilder.DropIndex(
                name: "IX_TourSessions_TourRequestId",
                table: "TourSessions");

            migrationBuilder.DropIndex(
                name: "IX_TourRequests_AssignedVolunteerId",
                table: "TourRequests");

            migrationBuilder.DropIndex(
                name: "IX_TourRequests_CreatedAt",
                table: "TourRequests");

            migrationBuilder.DropIndex(
                name: "IX_TourRequests_RequesterId",
                table: "TourRequests");

            migrationBuilder.DropIndex(
                name: "IX_TourRequests_Status_ExpiresAt",
                table: "TourRequests");

            migrationBuilder.DropIndex(
                name: "IX_TourParticipants_TourSessionId_UserId",
                table: "TourParticipants");

            migrationBuilder.DropIndex(
                name: "IX_TourParticipants_UserId",
                table: "TourParticipants");

            migrationBuilder.DropIndex(
                name: "IX_TipTransactions_CreatedAt",
                table: "TipTransactions");

            migrationBuilder.DropIndex(
                name: "IX_TipTransactions_IdempotencyKey",
                table: "TipTransactions");

            migrationBuilder.DropIndex(
                name: "IX_TipTransactions_ReceiverId",
                table: "TipTransactions");

            migrationBuilder.DropIndex(
                name: "IX_TipTransactions_SenderId",
                table: "TipTransactions");

            migrationBuilder.DropIndex(
                name: "IX_TipTransactions_TourSessionId",
                table: "TipTransactions");

            migrationBuilder.DropIndex(
                name: "IX_ScheduledTours_ProposedByUserId",
                table: "ScheduledTours");

            migrationBuilder.DropIndex(
                name: "IX_ScheduledTours_ProposedStartUtc",
                table: "ScheduledTours");

            migrationBuilder.DropIndex(
                name: "IX_ScheduledTours_Status",
                table: "ScheduledTours");

            migrationBuilder.DropIndex(
                name: "IX_ScheduledTours_TourRequestId",
                table: "ScheduledTours");

            migrationBuilder.DropIndex(
                name: "IX_Reports_CreatedAt",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_ReportedUserId",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_ReporterId",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_Status",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_TourSessionId",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_ReliabilityEvents_CreatedAt",
                table: "ReliabilityEvents");

            migrationBuilder.DropIndex(
                name: "IX_ReliabilityEvents_RelatedSessionId",
                table: "ReliabilityEvents");

            migrationBuilder.DropIndex(
                name: "IX_ReliabilityEvents_Type",
                table: "ReliabilityEvents");

            migrationBuilder.DropIndex(
                name: "IX_ReliabilityEvents_UserId",
                table: "ReliabilityEvents");

            migrationBuilder.DropIndex(
                name: "IX_Feedbacks_CreatedAt",
                table: "Feedbacks");

            migrationBuilder.DropIndex(
                name: "IX_Feedbacks_ReviewerId",
                table: "Feedbacks");

            migrationBuilder.DropIndex(
                name: "IX_Feedbacks_TargetUserId",
                table: "Feedbacks");

            migrationBuilder.DropIndex(
                name: "IX_Feedbacks_TourSessionId_ReviewerId_TargetUserId",
                table: "Feedbacks");

            migrationBuilder.DropIndex(
                name: "IX_Donations_CreatedAt",
                table: "Donations");

            migrationBuilder.DropIndex(
                name: "IX_Donations_DonorId",
                table: "Donations");

            migrationBuilder.DropIndex(
                name: "IX_Donations_IdempotencyKey",
                table: "Donations");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "TipTransactions");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Donations");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "TourRequests",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<Guid>(
                name: "TourSessionId",
                table: "TipTransactions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "SenderId",
                table: "TipTransactions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Guid>(
                name: "ReceiverId",
                table: "TipTransactions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "TipTransactions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "TipTransactions",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "TipTransactions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Reports",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<Guid>(
                name: "TourSessionId",
                table: "Feedbacks",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Guid>(
                name: "TargetUserId",
                table: "Feedbacks",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Guid>(
                name: "ReviewerId",
                table: "Feedbacks",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Comments",
                table: "Feedbacks",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "Feedbacks",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Guid>(
                name: "DonorId",
                table: "Donations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "Donations",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Donations",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "Donations",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_TourParticipants_TourSessionId",
                table: "TourParticipants",
                column: "TourSessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_AspNetUsers_SenderId",
                table: "ChatMessages",
                column: "SenderId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ModerationAuditLogs_AspNetUsers_ModeratorId",
                table: "ModerationAuditLogs",
                column: "ModeratorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TourRequests_Places_PlaceId",
                table: "TourRequests",
                column: "PlaceId",
                principalTable: "Places",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
