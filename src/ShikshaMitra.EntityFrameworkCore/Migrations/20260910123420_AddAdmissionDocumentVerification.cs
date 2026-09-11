using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddAdmissionDocumentVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAdmissionDocumentEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequirementLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequirementKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CollectionMode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DocumentTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Size = table.Column<long>(type: "bigint", nullable: true),
                    Sha256Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    BlobName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    FileSubmittedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalSeenBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubmittedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    PolicySnapshotJson = table.Column<string>(type: "text", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentLinkedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentLinkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_AppAdmissionDocumentEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAdmissionDocumentEvidence_AppAdmissionApplicationStageIn~",
                        column: x => x.StageInstanceId,
                        principalTable: "AppAdmissionApplicationStageInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppAdmissionDocumentEvidence_AppAdmissionApplications_Appli~",
                        column: x => x.ApplicationId,
                        principalTable: "AppAdmissionApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppAdmissionDocumentEvidence_AppDocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "AppDocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppAdmissionDocumentEvidence_AppStudents_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AppStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppAdmissionDocumentReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_AppAdmissionDocumentReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAdmissionDocumentReviews_AppAdmissionApplicationStageIns~",
                        column: x => x.StageInstanceId,
                        principalTable: "AppAdmissionApplicationStageInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppAdmissionDocumentReviews_AppAdmissionApplications_Applic~",
                        column: x => x.ApplicationId,
                        principalTable: "AppAdmissionApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppAdmissionDocumentReviews_AppAdmissionDocumentEvidence_Ev~",
                        column: x => x.EvidenceId,
                        principalTable: "AppAdmissionDocumentEvidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionDocuments_Application_Stage",
                table: "AppAdmissionDocumentEvidence",
                columns: new[] { "TenantId", "ApplicationId", "StageInstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionDocuments_Student",
                table: "AppAdmissionDocumentEvidence",
                columns: new[] { "TenantId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionDocumentEvidence_ApplicationId",
                table: "AppAdmissionDocumentEvidence",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionDocumentEvidence_DocumentTypeId",
                table: "AppAdmissionDocumentEvidence",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionDocumentEvidence_StudentId",
                table: "AppAdmissionDocumentEvidence",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "UX_AdmissionDocuments_Stage_Requirement_Version",
                table: "AppAdmissionDocumentEvidence",
                columns: new[] { "StageInstanceId", "RequirementCode", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdmissionDocumentReviews_Application_Stage",
                table: "AppAdmissionDocumentReviews",
                columns: new[] { "TenantId", "ApplicationId", "StageInstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionDocumentReviews_ApplicationId",
                table: "AppAdmissionDocumentReviews",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionDocumentReviews_StageInstanceId",
                table: "AppAdmissionDocumentReviews",
                column: "StageInstanceId");

            migrationBuilder.CreateIndex(
                name: "UX_AdmissionDocumentReviews_Evidence",
                table: "AppAdmissionDocumentReviews",
                column: "EvidenceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAdmissionDocumentReviews");

            migrationBuilder.DropTable(
                name: "AppAdmissionDocumentEvidence");
        }
    }
}
