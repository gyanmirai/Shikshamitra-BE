using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v12 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Amount",
                table: "ConcessionTypes");

            migrationBuilder.DropColumn(
                name: "BasedOn",
                table: "ConcessionTypes");

            migrationBuilder.CreateTable(
                name: "ConcessionRequestFeeHeadValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcessionRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcessionTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeHeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    BasedOn = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<double>(type: "double precision", nullable: false),
                    DiscountAmount = table.Column<double>(type: "double precision", nullable: false),
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
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    EducationMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeStructureId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYear = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentStep = table.Column<int>(type: "integer", nullable: false),
                    CompletedStepsRaw = table.Column<string>(type: "text", nullable: false),
                    ApproverId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApproverMessage = table.Column<string>(type: "text", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectorId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_ConcessionRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConcessionRequestConcessionTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcessionRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcessionTypeId = table.Column<Guid>(type: "uuid", nullable: false)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConcessionRequestFeeHeadMappings");

            migrationBuilder.DropTable(
                name: "ConcessionRequestFeeHeadValues");

            migrationBuilder.DropTable(
                name: "ConcessionRequestConcessionTypes");

            migrationBuilder.DropTable(
                name: "ConcessionRequests");

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "ConcessionTypes",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "BasedOn",
                table: "ConcessionTypes",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
