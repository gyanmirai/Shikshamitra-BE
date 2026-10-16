using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentRollNumberBatchWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppStudentRollNumberBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    RosterHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RuleJson = table.Column<string>(type: "jsonb", nullable: false),
                    RowsJson = table.Column<string>(type: "jsonb", nullable: false),
                    PolicyJson = table.Column<string>(type: "jsonb", nullable: false),
                    ApprovalStagesJson = table.Column<string>(type: "jsonb", nullable: false),
                    DecisionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SubmissionKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PreviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AppliedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_AppStudentRollNumberBatches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRollNumberBatches_TenantId_AcademicYearId_Divisio~",
                table: "AppStudentRollNumberBatches",
                columns: new[] { "TenantId", "AcademicYearId", "DivisionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRollNumberBatches_TenantId_RequestedBy_Submission~",
                table: "AppStudentRollNumberBatches",
                columns: new[] { "TenantId", "RequestedBy", "SubmissionKey" },
                unique: true,
                filter: "\"SubmissionKey\" IS NOT NULL AND \"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppStudentRollNumberBatches");
        }
    }
}
