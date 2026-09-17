using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class concessionRequestManage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Finance_ConcessionRequests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CancelledBy",
                table: "Finance_ConcessionRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledOn",
                table: "Finance_ConcessionRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreationIdempotencyKey",
                table: "Finance_ConcessionRequests",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreationPayloadHash",
                table: "Finance_ConcessionRequests",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestInputSnapshotJson",
                table: "Finance_ConcessionRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectedDueFeeIdsJson",
                table: "Finance_ConcessionRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceRequestId",
                table: "Finance_ConcessionRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedOn",
                table: "Finance_ConcessionRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TypeRequirementsSnapshotJson",
                table: "Finance_ConcessionRequests",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Finance_ConcessionCommandReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConcessionRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PayloadHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_ConcessionCommandReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finance_ConcessionCommandReceipts_Finance_ConcessionRequest~",
                        column: x => x.ConcessionRequestId,
                        principalTable: "Finance_ConcessionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Finance_ConcessionDocumentUploads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConcessionTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ExpiresOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConcessionRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttachedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_ConcessionDocumentUploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finance_ConcessionDocumentUploads_Finance_ConcessionRequest~",
                        column: x => x.ConcessionRequestId,
                        principalTable: "Finance_ConcessionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_ConcessionRequest_PendingStudentYearType",
                table: "Finance_ConcessionRequests",
                columns: new[] { "TenantId", "StudentId", "AcademicYearId", "ConcessionTypeId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" = 0")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionCommandReceipt_Request",
                table: "Finance_ConcessionCommandReceipts",
                column: "ConcessionRequestId");

            migrationBuilder.CreateIndex(
                name: "UX_ConcessionCommandReceipt_Tenant_Actor_Action_Key",
                table: "Finance_ConcessionCommandReceipts",
                columns: new[] { "TenantId", "ActorId", "Action", "IdempotencyKey" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionDocumentUpload_Expiry",
                table: "Finance_ConcessionDocumentUploads",
                columns: new[] { "TenantId", "ExpiresOn", "ConcessionRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionDocumentUpload_Owner_Requirement",
                table: "Finance_ConcessionDocumentUploads",
                columns: new[] { "TenantId", "UploadedBy", "ConcessionTypeId", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ConcessionDocumentUploads_ConcessionRequestId",
                table: "Finance_ConcessionDocumentUploads",
                column: "ConcessionRequestId");

            migrationBuilder.CreateIndex(
                name: "UX_ConcessionDocumentUpload_Tenant_Url",
                table: "Finance_ConcessionDocumentUploads",
                columns: new[] { "TenantId", "Url" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Finance_ConcessionCommandReceipts");

            migrationBuilder.DropTable(
                name: "Finance_ConcessionDocumentUploads");

            migrationBuilder.DropIndex(
                name: "UX_ConcessionRequest_PendingStudentYearType",
                table: "Finance_ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Finance_ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "CancelledBy",
                table: "Finance_ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "CancelledOn",
                table: "Finance_ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "CreationIdempotencyKey",
                table: "Finance_ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "CreationPayloadHash",
                table: "Finance_ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "RequestInputSnapshotJson",
                table: "Finance_ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "SelectedDueFeeIdsJson",
                table: "Finance_ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "SourceRequestId",
                table: "Finance_ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "SubmittedOn",
                table: "Finance_ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "TypeRequirementsSnapshotJson",
                table: "Finance_ConcessionRequests");
        }
    }
}
