using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class Fix_ConcessionTypeDocumentFormat_CompositeKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConcessionRequestDocuments");

            migrationBuilder.DropTable(
                name: "ConcessionRequestFeeHeadMappings");

            migrationBuilder.DropTable(
                name: "ConcessionRequestFeeHeadValues");

            migrationBuilder.DropTable(
                name: "ConcessionRequestConcessionTypes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppConcessionTypeDocumentFormats",
                table: "AppConcessionTypeDocumentFormats");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ConcessionRequests",
                table: "ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "CompletedStepsRaw",
                table: "ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "FeeStructureId",
                table: "ConcessionRequests");

            migrationBuilder.RenameTable(
                name: "ConcessionRequests",
                newName: "AppConcessionRequests");

            migrationBuilder.RenameColumn(
                name: "CurrentStep",
                table: "AppConcessionRequests",
                newName: "Status");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcessionTypeId",
                table: "AppConcessionRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "AppConcessionRequests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "AppConcessionRequests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppConcessionTypeDocumentFormats",
                table: "AppConcessionTypeDocumentFormats",
                columns: new[] { "ConcessionTypeDocumentId", "DocumentType" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppConcessionRequests",
                table: "AppConcessionRequests",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "AppConcessionFeeHeadMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConcessionRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeHeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeHeadConcessionAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PreConcessionFeeHeadAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PostConcessionFeeHeadAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UserSpecifiedConcessionAmountValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UserSpecifiedConcessionAmountType = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppConcessionFeeHeadMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppConcessionFeeHeadMappings_AppConcessionRequests_Concessi~",
                        column: x => x.ConcessionRequestId,
                        principalTable: "AppConcessionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionRequests_ConcessionTypeId",
                table: "AppConcessionRequests",
                column: "ConcessionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionRequests_StudentId",
                table: "AppConcessionRequests",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionRequests_StudentId_Status",
                table: "AppConcessionRequests",
                columns: new[] { "StudentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionFeeHeadMappings_ConcessionRequestId",
                table: "AppConcessionFeeHeadMappings",
                column: "ConcessionRequestId");

            migrationBuilder.CreateIndex(
                name: "UIX_ConcessionFeeHeadMappings_RequestId_FeeHeadId",
                table: "AppConcessionFeeHeadMappings",
                columns: new[] { "ConcessionRequestId", "FeeHeadId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppConcessionFeeHeadMappings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppConcessionTypeDocumentFormats",
                table: "AppConcessionTypeDocumentFormats");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AppConcessionRequests",
                table: "AppConcessionRequests");

            migrationBuilder.DropIndex(
                name: "IX_ConcessionRequests_ConcessionTypeId",
                table: "AppConcessionRequests");

            migrationBuilder.DropIndex(
                name: "IX_ConcessionRequests_StudentId",
                table: "AppConcessionRequests");

            migrationBuilder.DropIndex(
                name: "IX_ConcessionRequests_StudentId_Status",
                table: "AppConcessionRequests");

            migrationBuilder.DropColumn(
                name: "ConcessionTypeId",
                table: "AppConcessionRequests");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "AppConcessionRequests");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "AppConcessionRequests");

            migrationBuilder.RenameTable(
                name: "AppConcessionRequests",
                newName: "ConcessionRequests");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "ConcessionRequests",
                newName: "CurrentStep");

            migrationBuilder.AddColumn<string>(
                name: "CompletedStepsRaw",
                table: "ConcessionRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "FeeStructureId",
                table: "ConcessionRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_AppConcessionTypeDocumentFormats",
                table: "AppConcessionTypeDocumentFormats",
                column: "ConcessionTypeDocumentId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ConcessionRequests",
                table: "ConcessionRequests",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "ConcessionRequestConcessionTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApproverId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApproverMessage = table.Column<string>(type: "text", nullable: true),
                    ConcessionRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcessionTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    RejectorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConcessionRequestConcessionTypes", x => x.Id);
                    table.UniqueConstraint("AK_ConcessionRequestConcessionTypes_ConcessionRequestId_Conces~", x => new { x.ConcessionRequestId, x.ConcessionTypeId });
                    table.ForeignKey(
                        name: "FK_ConcessionRequestConcessionTypes_ConcessionRequests_Concess~",
                        column: x => x.ConcessionRequestId,
                        principalTable: "ConcessionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConcessionRequestDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcessionRequestConcessionTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcessionRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcessionTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Url = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConcessionRequestDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConcessionRequestFeeHeadValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<double>(type: "double precision", nullable: false),
                    BasedOn = table.Column<int>(type: "integer", nullable: false),
                    ConcessionRequestConcessionTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcessionRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcessionTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscountAmount = table.Column<double>(type: "double precision", nullable: false),
                    FeeHeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    NetAmount = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConcessionRequestFeeHeadValues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConcessionRequestFeeHeadMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcessionRequestConcessionTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcessionRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcessionTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeHeadId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConcessionRequestFeeHeadMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConcessionRequestFeeHeadMappings_ConcessionRequestConcessio~",
                        columns: x => new { x.ConcessionRequestId, x.ConcessionTypeId },
                        principalTable: "ConcessionRequestConcessionTypes",
                        principalColumns: new[] { "ConcessionRequestId", "ConcessionTypeId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionRequestConcessionTypes_ConcessionRequestId_Conces~",
                table: "ConcessionRequestConcessionTypes",
                columns: new[] { "ConcessionRequestId", "ConcessionTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionRequestFeeHeadMappings_ConcessionRequestId_Conces~",
                table: "ConcessionRequestFeeHeadMappings",
                columns: new[] { "ConcessionRequestId", "ConcessionTypeId", "FeeHeadId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionRequestFeeHeadValues_ConcessionRequestId_Concessi~",
                table: "ConcessionRequestFeeHeadValues",
                columns: new[] { "ConcessionRequestId", "ConcessionTypeId", "FeeHeadId" },
                unique: true);
        }
    }
}
