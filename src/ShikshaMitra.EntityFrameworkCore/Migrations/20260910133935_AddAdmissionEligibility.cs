using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionEligibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAdmissionEligibilityEvaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PolicySnapshotJson = table.Column<string>(type: "text", nullable: false),
                    FactsSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    CriteriaSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    InputFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EvaluatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_AppAdmissionEligibilityEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAdmissionEligibilityEvaluations_AppAdmissionApplicationS~",
                        column: x => x.StageInstanceId,
                        principalTable: "AppAdmissionApplicationStageInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppAdmissionEligibilityEvaluations_AppAdmissionApplications~",
                        column: x => x.ApplicationId,
                        principalTable: "AppAdmissionApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppAdmissionEligibilityExceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Decision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_AppAdmissionEligibilityExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAdmissionEligibilityExceptions_AppAdmissionApplicationSt~",
                        column: x => x.StageInstanceId,
                        principalTable: "AppAdmissionApplicationStageInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppAdmissionEligibilityExceptions_AppAdmissionApplications_~",
                        column: x => x.ApplicationId,
                        principalTable: "AppAdmissionApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppAdmissionEligibilityExceptions_AppAdmissionEligibilityEv~",
                        column: x => x.EvaluationId,
                        principalTable: "AppAdmissionEligibilityEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppAdmissionEligibilityExceptions_AppAdmissionEligibilityEx~",
                        column: x => x.RequestId,
                        principalTable: "AppAdmissionEligibilityExceptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionEligibility_Application_Stage",
                table: "AppAdmissionEligibilityEvaluations",
                columns: new[] { "TenantId", "ApplicationId", "StageInstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionEligibilityEvaluations_ApplicationId",
                table: "AppAdmissionEligibilityEvaluations",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "UX_AdmissionEligibility_Stage_Sequence",
                table: "AppAdmissionEligibilityEvaluations",
                columns: new[] { "StageInstanceId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionEligibilityException_Application_Stage",
                table: "AppAdmissionEligibilityExceptions",
                columns: new[] { "TenantId", "ApplicationId", "StageInstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionEligibilityExceptions_ApplicationId",
                table: "AppAdmissionEligibilityExceptions",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionEligibilityExceptions_EvaluationId",
                table: "AppAdmissionEligibilityExceptions",
                column: "EvaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionEligibilityExceptions_StageInstanceId",
                table: "AppAdmissionEligibilityExceptions",
                column: "StageInstanceId");

            migrationBuilder.CreateIndex(
                name: "UX_AdmissionEligibilityException_Request",
                table: "AppAdmissionEligibilityExceptions",
                column: "RequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAdmissionEligibilityExceptions");

            migrationBuilder.DropTable(
                name: "AppAdmissionEligibilityEvaluations");
        }
    }
}
