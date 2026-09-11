using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionAssessments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAdmissionAssessmentSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdmissionOfferingId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowStageDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Location = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    BookedCount = table.Column<int>(type: "integer", nullable: false),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAdmissionAssessmentSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAdmissionAssessmentSessions_AppAdmissionOfferings_Admiss~",
                        column: x => x.AdmissionOfferingId,
                        principalTable: "AppAdmissionOfferings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppAdmissionAssessmentSessions_AppAdmissionWorkflowStages_W~",
                        column: x => x.WorkflowStageDefinitionId,
                        principalTable: "AppAdmissionWorkflowStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppAdmissionAssessmentAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    Attendance = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Marks = table.Column<decimal>(type: "numeric", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    RecordedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PolicySnapshotJson = table.Column<string>(type: "text", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAdmissionAssessmentAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAdmissionAssessmentAttempts_AppAdmissionApplicationStage~",
                        column: x => x.StageInstanceId,
                        principalTable: "AppAdmissionApplicationStageInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppAdmissionAssessmentAttempts_AppAdmissionApplications_App~",
                        column: x => x.ApplicationId,
                        principalTable: "AppAdmissionApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppAdmissionAssessmentAttempts_AppAdmissionAssessmentSessio~",
                        column: x => x.SessionId,
                        principalTable: "AppAdmissionAssessmentSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionAssessmentAttempts_ApplicationId",
                table: "AppAdmissionAssessmentAttempts",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionAssessmentAttempts_SessionId",
                table: "AppAdmissionAssessmentAttempts",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentAttempts_Application_Stage",
                table: "AppAdmissionAssessmentAttempts",
                columns: new[] { "TenantId", "ApplicationId", "StageInstanceId" });

            migrationBuilder.CreateIndex(
                name: "UX_AssessmentAttempts_Stage_Number",
                table: "AppAdmissionAssessmentAttempts",
                columns: new[] { "StageInstanceId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionAssessmentSessions_AdmissionOfferingId",
                table: "AppAdmissionAssessmentSessions",
                column: "AdmissionOfferingId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionAssessmentSessions_WorkflowStageDefinitionId",
                table: "AppAdmissionAssessmentSessions",
                column: "WorkflowStageDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentSessions_Offering_Stage_Start",
                table: "AppAdmissionAssessmentSessions",
                columns: new[] { "TenantId", "AdmissionOfferingId", "WorkflowStageDefinitionId", "StartsAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAdmissionAssessmentAttempts");

            migrationBuilder.DropTable(
                name: "AppAdmissionAssessmentSessions");
        }
    }
}
