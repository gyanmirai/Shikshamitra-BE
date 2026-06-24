using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class StudentCertificateFramework : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppStudentCertificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CertificateType = table.Column<int>(type: "integer", nullable: false),
                    IssueDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GeneratedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CertificateDataJson = table.Column<string>(type: "jsonb", nullable: false),
                    TemplateSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
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
                    table.PrimaryKey("PK_AppStudentCertificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentCertificates_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentCertificateTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CertificateType = table.Column<int>(type: "integer", nullable: false),
                    HeaderContent = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    FooterContent = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    LogoUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SchoolAddress = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PrincipalSignatureUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SchoolStampUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppStudentCertificateTemplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentCertificates_StudentId",
                table: "AppStudentCertificates",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentCertificates_TenantId_CertificateType_Certificate~",
                table: "AppStudentCertificates",
                columns: new[] { "TenantId", "CertificateType", "CertificateNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentCertificates_TenantId_StudentId_CreationTime",
                table: "AppStudentCertificates",
                columns: new[] { "TenantId", "StudentId", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentCertificateTemplates_TenantId_CertificateType_IsA~",
                table: "AppStudentCertificateTemplates",
                columns: new[] { "TenantId", "CertificateType", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppStudentCertificates");

            migrationBuilder.DropTable(
                name: "AppStudentCertificateTemplates");
        }
    }
}
