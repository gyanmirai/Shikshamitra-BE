using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionInquiryManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAdmissionInquiryManagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    InquiryId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicySnapshotJson = table.Column<string>(type: "text", nullable: false),
                    FirstContactDueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    QualificationDueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastContactAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ContactCount = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: true),
                    QualificationAnswersJson = table.Column<string>(type: "text", nullable: true),
                    OutcomeReason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    OutcomeAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OutcomeBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppAdmissionInquiryManagement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAdmissionInquiryManagement_AppAdmissionInquiries_Inquiry~",
                        column: x => x.InquiryId,
                        principalTable: "AppAdmissionInquiries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppAdmissionInquiryManagement_AppAdmissionWorkflowStages_St~",
                        column: x => x.StageDefinitionId,
                        principalTable: "AppAdmissionWorkflowStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppAdmissionInquiryFollowUps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    InquiryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManagementId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedCounsellorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletionNote = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
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
                    table.PrimaryKey("PK_AppAdmissionInquiryFollowUps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAdmissionInquiryFollowUps_AppAdmissionInquiries_InquiryId",
                        column: x => x.InquiryId,
                        principalTable: "AppAdmissionInquiries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppAdmissionInquiryFollowUps_AppAdmissionInquiryManagement_~",
                        column: x => x.ManagementId,
                        principalTable: "AppAdmissionInquiryManagement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionInquiryFollowUp_Assigned_Status_Due",
                table: "AppAdmissionInquiryFollowUps",
                columns: new[] { "TenantId", "AssignedCounsellorId", "Status", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionInquiryFollowUp_Inquiry_Status_Due",
                table: "AppAdmissionInquiryFollowUps",
                columns: new[] { "TenantId", "InquiryId", "Status", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionInquiryFollowUps_InquiryId",
                table: "AppAdmissionInquiryFollowUps",
                column: "InquiryId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionInquiryFollowUps_ManagementId",
                table: "AppAdmissionInquiryFollowUps",
                column: "ManagementId");

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionInquiryManagement_FirstContactDue",
                table: "AppAdmissionInquiryManagement",
                columns: new[] { "TenantId", "FirstContactDueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionInquiryManagement_Outcome",
                table: "AppAdmissionInquiryManagement",
                columns: new[] { "TenantId", "Outcome" });

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionInquiryManagement_QualificationDue",
                table: "AppAdmissionInquiryManagement",
                columns: new[] { "TenantId", "QualificationDueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionInquiryManagement_StageDefinitionId",
                table: "AppAdmissionInquiryManagement",
                column: "StageDefinitionId");

            migrationBuilder.CreateIndex(
                name: "UX_AdmissionInquiryManagement_Inquiry",
                table: "AppAdmissionInquiryManagement",
                column: "InquiryId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAdmissionInquiryFollowUps");

            migrationBuilder.DropTable(
                name: "AppAdmissionInquiryManagement");
        }
    }
}
