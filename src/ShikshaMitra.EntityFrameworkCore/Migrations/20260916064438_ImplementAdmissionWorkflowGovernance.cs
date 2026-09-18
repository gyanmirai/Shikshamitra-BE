using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class ImplementAdmissionWorkflowGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "AppAdmissionWorkflowDefinitions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedBy",
                table: "AppAdmissionWorkflowDefinitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedApproverId",
                table: "AppAdmissionWorkflowDefinitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangeReason",
                table: "AppAdmissionWorkflowDefinitions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "AppAdmissionWorkflowDefinitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RollbackSourceVersionId",
                table: "AppAdmissionWorkflowDefinitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledPublishAt",
                table: "AppAdmissionWorkflowDefinitions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "AppAdmissionWorkflowDefinitions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedBy",
                table: "AppAdmissionWorkflowDefinitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidatedAt",
                table: "AppAdmissionWorkflowDefinitions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ValidatedBy",
                table: "AppAdmissionWorkflowDefinitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppAdmissionWorkflowGovernanceHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: true),
                    ToStatus = table.Column<int>(type: "integer", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedApproverId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EffectiveAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAdmissionWorkflowGovernanceHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAdmissionWorkflowGovernanceHistories_AppAdmissionWorkflo~",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "AppAdmissionWorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppAdmissionWorkflowGovernanceSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    MakerCheckerEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    InAppNotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    EmailNotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppAdmissionWorkflowGovernanceSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAdmissionWorkflowNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAdmissionWorkflowNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAdmissionWorkflowNotifications_AppAdmissionWorkflowDefin~",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "AppAdmissionWorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionWorkflowGovernanceHistories_TenantId_WorkflowDe~",
                table: "AppAdmissionWorkflowGovernanceHistories",
                columns: new[] { "TenantId", "WorkflowDefinitionId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionWorkflowGovernanceHistories_WorkflowDefinitionId",
                table: "AppAdmissionWorkflowGovernanceHistories",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionWorkflowGovernanceSettings_TenantId",
                table: "AppAdmissionWorkflowGovernanceSettings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionWorkflowNotifications_TenantId_RecipientUserId_~",
                table: "AppAdmissionWorkflowNotifications",
                columns: new[] { "TenantId", "RecipientUserId", "IsRead", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionWorkflowNotifications_WorkflowDefinitionId",
                table: "AppAdmissionWorkflowNotifications",
                column: "WorkflowDefinitionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAdmissionWorkflowGovernanceHistories");

            migrationBuilder.DropTable(
                name: "AppAdmissionWorkflowGovernanceSettings");

            migrationBuilder.DropTable(
                name: "AppAdmissionWorkflowNotifications");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "AppAdmissionWorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "AppAdmissionWorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "AssignedApproverId",
                table: "AppAdmissionWorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "ChangeReason",
                table: "AppAdmissionWorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "AppAdmissionWorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "RollbackSourceVersionId",
                table: "AppAdmissionWorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "ScheduledPublishAt",
                table: "AppAdmissionWorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "AppAdmissionWorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "SubmittedBy",
                table: "AppAdmissionWorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "ValidatedAt",
                table: "AppAdmissionWorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "ValidatedBy",
                table: "AppAdmissionWorkflowDefinitions");
        }
    }
}
