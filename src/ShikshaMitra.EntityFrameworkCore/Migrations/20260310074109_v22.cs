using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v22 : Migration
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

            migrationBuilder.DropTable(
                name: "ConcessionRequests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "ConcessionRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedStepsRaw = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentStep = table.Column<int>(type: "integer", nullable: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    FeeStructureId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConcessionRequests", x => x.Id);
                });

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
