using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class StudentComplianceAndDocumentHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DownloadCount",
                table: "AppStudentDocuments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsSensitive",
                table: "AppStudentDocuments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastDownloadedAt",
                table: "AppStudentDocuments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sha256Hash",
                table: "AppStudentDocuments",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "StorageStatus",
                table: "AppStudentDocuments",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "UploadedByUserId",
                table: "AppStudentDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ValidatedAt",
                table: "AppStudentDocuments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppStudentDocumentAccessAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Size = table.Column<long>(type: "bigint", nullable: true),
                    Sha256Hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ReasonCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentDocumentAccessAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentIdentityCompliances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AadhaarEncrypted = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    AadhaarHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AadhaarLast4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    AadhaarName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AadhaarVerificationStatus = table.Column<int>(type: "integer", nullable: false),
                    ApaarId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ApaarConsentStatus = table.Column<int>(type: "integer", nullable: false),
                    ApaarConsentDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApaarVerificationStatus = table.Column<int>(type: "integer", nullable: false),
                    PenNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    UdiseStudentCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UdiseSyncStatus = table.Column<int>(type: "integer", nullable: false),
                    UdiseLastExportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RteStatus = table.Column<bool>(type: "boolean", nullable: false),
                    ScholarshipStatus = table.Column<int>(type: "integer", nullable: false),
                    ScholarshipSchemeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ScholarshipApplicationNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CasteCertificateNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CasteCertificateVerifiedStatus = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppStudentIdentityCompliances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentIdentityCompliances_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentDocuments_StudentId_DocumentTypeId_Sha256Hash",
                table: "AppStudentDocuments",
                columns: new[] { "StudentId", "DocumentTypeId", "Sha256Hash" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentDocumentAccessAudits_TenantId_Action_EventTime",
                table: "AppStudentDocumentAccessAudits",
                columns: new[] { "TenantId", "Action", "EventTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentDocumentAccessAudits_TenantId_DocumentId_EventTime",
                table: "AppStudentDocumentAccessAudits",
                columns: new[] { "TenantId", "DocumentId", "EventTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentDocumentAccessAudits_TenantId_StudentId_EventTime",
                table: "AppStudentDocumentAccessAudits",
                columns: new[] { "TenantId", "StudentId", "EventTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentIdentityCompliances_StudentId",
                table: "AppStudentIdentityCompliances",
                column: "StudentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentIdentityCompliances_TenantId_AadhaarHash",
                table: "AppStudentIdentityCompliances",
                columns: new[] { "TenantId", "AadhaarHash" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentIdentityCompliances_TenantId_ApaarId",
                table: "AppStudentIdentityCompliances",
                columns: new[] { "TenantId", "ApaarId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentIdentityCompliances_TenantId_PenNumber",
                table: "AppStudentIdentityCompliances",
                columns: new[] { "TenantId", "PenNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentIdentityCompliances_TenantId_UdiseSyncStatus",
                table: "AppStudentIdentityCompliances",
                columns: new[] { "TenantId", "UdiseSyncStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppStudentDocumentAccessAudits");

            migrationBuilder.DropTable(
                name: "AppStudentIdentityCompliances");

            migrationBuilder.DropIndex(
                name: "IX_AppStudentDocuments_StudentId_DocumentTypeId_Sha256Hash",
                table: "AppStudentDocuments");

            migrationBuilder.DropColumn(
                name: "DownloadCount",
                table: "AppStudentDocuments");

            migrationBuilder.DropColumn(
                name: "IsSensitive",
                table: "AppStudentDocuments");

            migrationBuilder.DropColumn(
                name: "LastDownloadedAt",
                table: "AppStudentDocuments");

            migrationBuilder.DropColumn(
                name: "Sha256Hash",
                table: "AppStudentDocuments");

            migrationBuilder.DropColumn(
                name: "StorageStatus",
                table: "AppStudentDocuments");

            migrationBuilder.DropColumn(
                name: "UploadedByUserId",
                table: "AppStudentDocuments");

            migrationBuilder.DropColumn(
                name: "ValidatedAt",
                table: "AppStudentDocuments");
        }
    }
}
