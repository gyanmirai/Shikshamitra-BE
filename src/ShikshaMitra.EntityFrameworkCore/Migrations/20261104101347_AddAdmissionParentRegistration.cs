using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionParentRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "SubmittedBy",
                table: "AppAdmissionDocumentEvidence",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "ParentContactJson",
                table: "AppAdmissionDocumentEvidence",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentRegistrationId",
                table: "AppAdmissionDocumentEvidence",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppAdmissionParentRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProtectedToken = table.Column<string>(type: "text", nullable: false),
                    RecipientJson = table.Column<string>(type: "text", nullable: false),
                    RecipientFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DeliveryStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DeliveryError = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    AllowsDocuments = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    HandedInAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveryStartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    FieldCodesJson = table.Column<string>(type: "text", nullable: false),
                    DocumentsJson = table.Column<string>(type: "text", nullable: false),
                    ProposedValuesJson = table.Column<string>(type: "text", nullable: false),
                    ProposalBaseline = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OperationsJson = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_AppAdmissionParentRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAdmissionParentRegistrations_AppAdmissionApplications_Ap~",
                        column: x => x.ApplicationId,
                        principalTable: "AppAdmissionApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionDocumentEvidence_ParentRegistrationId",
                table: "AppAdmissionDocumentEvidence",
                column: "ParentRegistrationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionParentRegistrations_ApplicationId",
                table: "AppAdmissionParentRegistrations",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionParentRegistrations_TenantId_ApplicationId_Crea~",
                table: "AppAdmissionParentRegistrations",
                columns: new[] { "TenantId", "ApplicationId", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionParentRegistrations_TokenHash",
                table: "AppAdmissionParentRegistrations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AppAdmissionDocumentEvidence_AppAdmissionParentRegistration~",
                table: "AppAdmissionDocumentEvidence",
                column: "ParentRegistrationId",
                principalTable: "AppAdmissionParentRegistrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Parent evidence has no staff actor. Refuse a lossy rollback rather than
            // inventing one or dropping the attribution needed to interpret retained files.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "AppAdmissionDocumentEvidence" WHERE "SubmittedBy" IS NULL OR "ParentRegistrationId" IS NOT NULL) THEN
                    RAISE EXCEPTION 'Cannot remove parent registration while parent-authored evidence exists. Retain or explicitly migrate that evidence and its attribution before rollback.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_AppAdmissionDocumentEvidence_AppAdmissionParentRegistration~",
                table: "AppAdmissionDocumentEvidence");

            migrationBuilder.DropTable(
                name: "AppAdmissionParentRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_AppAdmissionDocumentEvidence_ParentRegistrationId",
                table: "AppAdmissionDocumentEvidence");

            migrationBuilder.DropColumn(
                name: "ParentContactJson",
                table: "AppAdmissionDocumentEvidence");

            migrationBuilder.DropColumn(
                name: "ParentRegistrationId",
                table: "AppAdmissionDocumentEvidence");

            migrationBuilder.AlterColumn<Guid>(
                name: "SubmittedBy",
                table: "AppAdmissionDocumentEvidence",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
