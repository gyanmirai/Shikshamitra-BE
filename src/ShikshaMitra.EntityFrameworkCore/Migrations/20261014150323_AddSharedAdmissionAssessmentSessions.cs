using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedAdmissionAssessmentSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BreakMinutes",
                table: "AppAdmissionAssessmentSessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InterviewerId",
                table: "AppAdmissionAssessmentSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RoomId",
                table: "AppAdmissionAssessmentSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RoomReservationId",
                table: "AppAdmissionAssessmentSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SessionType",
                table: "AppAdmissionAssessmentSessions",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "exam");

            migrationBuilder.AddColumn<int>(
                name: "SlotDurationMinutes",
                table: "AppAdmissionAssessmentSessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlotEndsAt",
                table: "AppAdmissionAssessmentAttempts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlotStartsAt",
                table: "AppAdmissionAssessmentAttempts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppAdmissionAssessmentBookingBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BookedCount = table.Column<int>(type: "integer", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAdmissionAssessmentBookingBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAdmissionAssessmentBookingBatches_AppAdmissionAssessment~",
                        column: x => x.SessionId,
                        principalTable: "AppAdmissionAssessmentSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppAdmissionAssessmentSessionScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdmissionOfferingId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowStageDefinitionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAdmissionAssessmentSessionScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAdmissionAssessmentSessionScopes_AppAdmissionAssessmentS~",
                        column: x => x.SessionId,
                        principalTable: "AppAdmissionAssessmentSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppAdmissionAssessmentSessionScopes_AppAdmissionOfferings_A~",
                        column: x => x.AdmissionOfferingId,
                        principalTable: "AppAdmissionOfferings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppAdmissionAssessmentSessionScopes_AppAdmissionWorkflowSta~",
                        column: x => x.WorkflowStageDefinitionId,
                        principalTable: "AppAdmissionWorkflowStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionAssessmentSessions_InterviewerId",
                table: "AppAdmissionAssessmentSessions",
                column: "InterviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionAssessmentSessions_RoomId",
                table: "AppAdmissionAssessmentSessions",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionAssessmentSessions_RoomReservationId",
                table: "AppAdmissionAssessmentSessions",
                column: "RoomReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionAssessmentSessions_TenantId_InterviewerId_Start~",
                table: "AppAdmissionAssessmentSessions",
                columns: new[] { "TenantId", "InterviewerId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionAssessmentSessions_TenantId_RoomId_StartsAt",
                table: "AppAdmissionAssessmentSessions",
                columns: new[] { "TenantId", "RoomId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "UX_AssessmentInterviewSlot",
                table: "AppAdmissionAssessmentAttempts",
                columns: new[] { "SessionId", "SlotStartsAt" },
                unique: true,
                filter: "\"SlotStartsAt\" IS NOT NULL AND \"Status\" <> 'cancelled' AND \"Outcome\" <> 'waived' AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionAssessmentBookingBatches_SessionId",
                table: "AppAdmissionAssessmentBookingBatches",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionAssessmentSessionScopes_AdmissionOfferingId",
                table: "AppAdmissionAssessmentSessionScopes",
                column: "AdmissionOfferingId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionAssessmentSessionScopes_TenantId_AdmissionOffer~",
                table: "AppAdmissionAssessmentSessionScopes",
                columns: new[] { "TenantId", "AdmissionOfferingId", "WorkflowStageDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionAssessmentSessionScopes_WorkflowStageDefinition~",
                table: "AppAdmissionAssessmentSessionScopes",
                column: "WorkflowStageDefinitionId");

            migrationBuilder.CreateIndex(
                name: "UX_AssessmentSessionScope",
                table: "AppAdmissionAssessmentSessionScopes",
                columns: new[] { "SessionId", "AdmissionOfferingId", "WorkflowStageDefinitionId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AppAdmissionAssessmentSessions_AppEmployees_InterviewerId",
                table: "AppAdmissionAssessmentSessions",
                column: "InterviewerId",
                principalTable: "AppEmployees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppAdmissionAssessmentSessions_academic_RoomReservations_Ro~",
                table: "AppAdmissionAssessmentSessions",
                column: "RoomReservationId",
                principalTable: "academic_RoomReservations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppAdmissionAssessmentSessions_academic_Rooms_RoomId",
                table: "AppAdmissionAssessmentSessions",
                column: "RoomId",
                principalTable: "academic_Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppAdmissionAssessmentSessions_AppEmployees_InterviewerId",
                table: "AppAdmissionAssessmentSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_AppAdmissionAssessmentSessions_academic_RoomReservations_Ro~",
                table: "AppAdmissionAssessmentSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_AppAdmissionAssessmentSessions_academic_Rooms_RoomId",
                table: "AppAdmissionAssessmentSessions");

            migrationBuilder.DropTable(
                name: "AppAdmissionAssessmentBookingBatches");

            migrationBuilder.DropTable(
                name: "AppAdmissionAssessmentSessionScopes");

            migrationBuilder.DropIndex(
                name: "IX_AppAdmissionAssessmentSessions_InterviewerId",
                table: "AppAdmissionAssessmentSessions");

            migrationBuilder.DropIndex(
                name: "IX_AppAdmissionAssessmentSessions_RoomId",
                table: "AppAdmissionAssessmentSessions");

            migrationBuilder.DropIndex(
                name: "IX_AppAdmissionAssessmentSessions_RoomReservationId",
                table: "AppAdmissionAssessmentSessions");

            migrationBuilder.DropIndex(
                name: "IX_AppAdmissionAssessmentSessions_TenantId_InterviewerId_Start~",
                table: "AppAdmissionAssessmentSessions");

            migrationBuilder.DropIndex(
                name: "IX_AppAdmissionAssessmentSessions_TenantId_RoomId_StartsAt",
                table: "AppAdmissionAssessmentSessions");

            migrationBuilder.DropIndex(
                name: "UX_AssessmentInterviewSlot",
                table: "AppAdmissionAssessmentAttempts");

            migrationBuilder.DropColumn(
                name: "BreakMinutes",
                table: "AppAdmissionAssessmentSessions");

            migrationBuilder.DropColumn(
                name: "InterviewerId",
                table: "AppAdmissionAssessmentSessions");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "AppAdmissionAssessmentSessions");

            migrationBuilder.DropColumn(
                name: "RoomReservationId",
                table: "AppAdmissionAssessmentSessions");

            migrationBuilder.DropColumn(
                name: "SessionType",
                table: "AppAdmissionAssessmentSessions");

            migrationBuilder.DropColumn(
                name: "SlotDurationMinutes",
                table: "AppAdmissionAssessmentSessions");

            migrationBuilder.DropColumn(
                name: "SlotEndsAt",
                table: "AppAdmissionAssessmentAttempts");

            migrationBuilder.DropColumn(
                name: "SlotStartsAt",
                table: "AppAdmissionAssessmentAttempts");

        }
    }
}
