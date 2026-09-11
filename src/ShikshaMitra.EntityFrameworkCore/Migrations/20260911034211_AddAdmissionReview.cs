using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAdmissionReviewRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Recommendation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Reason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    AssignedReviewerId = table.Column<Guid>(type: "uuid", nullable: true),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InputFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PolicySnapshotJson = table.Column<string>(type: "text", nullable: false),
                    EvidenceSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    ChecklistSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    RecordedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_AppAdmissionReviewRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAdmissionReviewRecords_AppAdmissionApplicationStageInsta~",
                        column: x => x.StageInstanceId,
                        principalTable: "AppAdmissionApplicationStageInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppAdmissionReviewRecords_AppAdmissionApplications_Applicat~",
                        column: x => x.ApplicationId,
                        principalTable: "AppAdmissionApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppAdmissionReviewWorkItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedReviewerId = table.Column<Guid>(type: "uuid", nullable: true),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
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
                    table.PrimaryKey("PK_AppAdmissionReviewWorkItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAdmissionReviewWorkItems_AppAdmissionApplicationStageIns~",
                        column: x => x.StageInstanceId,
                        principalTable: "AppAdmissionApplicationStageInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppAdmissionReviewWorkItems_AppAdmissionApplications_Applic~",
                        column: x => x.ApplicationId,
                        principalTable: "AppAdmissionApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppAdmissionReviewDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    DecidedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_AppAdmissionReviewDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAdmissionReviewDecisions_AppAdmissionApplicationStageIns~",
                        column: x => x.StageInstanceId,
                        principalTable: "AppAdmissionApplicationStageInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppAdmissionReviewDecisions_AppAdmissionApplications_Applic~",
                        column: x => x.ApplicationId,
                        principalTable: "AppAdmissionApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppAdmissionReviewDecisions_AppAdmissionReviewRecords_Recor~",
                        column: x => x.RecordId,
                        principalTable: "AppAdmissionReviewRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionReviewDecision_Application_Stage",
                table: "AppAdmissionReviewDecisions",
                columns: new[] { "TenantId", "ApplicationId", "StageInstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionReviewDecisions_ApplicationId",
                table: "AppAdmissionReviewDecisions",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionReviewDecisions_StageInstanceId",
                table: "AppAdmissionReviewDecisions",
                column: "StageInstanceId");

            migrationBuilder.CreateIndex(
                name: "UX_AdmissionReviewDecision_Record",
                table: "AppAdmissionReviewDecisions",
                column: "RecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionReviewRecord_Application_Stage",
                table: "AppAdmissionReviewRecords",
                columns: new[] { "TenantId", "ApplicationId", "StageInstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionReviewRecords_ApplicationId",
                table: "AppAdmissionReviewRecords",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "UX_AdmissionReviewRecord_Stage_Sequence",
                table: "AppAdmissionReviewRecords",
                columns: new[] { "StageInstanceId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionReviewWorkItem_Assigned_Due",
                table: "AppAdmissionReviewWorkItems",
                columns: new[] { "TenantId", "AssignedReviewerId", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionReviewWorkItems_ApplicationId",
                table: "AppAdmissionReviewWorkItems",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "UX_AdmissionReviewWorkItem_Stage",
                table: "AppAdmissionReviewWorkItems",
                column: "StageInstanceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAdmissionReviewDecisions");

            migrationBuilder.DropTable(
                name: "AppAdmissionReviewWorkItems");

            migrationBuilder.DropTable(
                name: "AppAdmissionReviewRecords");
        }
    }
}
