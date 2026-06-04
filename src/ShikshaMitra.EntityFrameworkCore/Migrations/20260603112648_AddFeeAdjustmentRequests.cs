using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddFeeAdjustmentRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AdjustmentCreditAmount",
                table: "DueFees",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateTable(
                name: "Finance_FeeAdjustmentRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    EducationMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdjustmentType = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Remarks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    VoucherNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RequestedAmount = table.Column<double>(type: "double precision", nullable: false),
                    AppliedAmount = table.Column<double>(type: "double precision", nullable: false),
                    RemainingAmount = table.Column<double>(type: "double precision", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ApproverNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ProcessedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApplyVersion = table.Column<int>(type: "integer", nullable: false),
                    LastIdempotencyKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_Finance_FeeAdjustmentRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Finance_FeeAdjustmentSourceLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeAdjustmentRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    DueFeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeHeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_FeeAdjustmentSourceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finance_FeeAdjustmentSourceLines_Finance_FeeAdjustmentReque~",
                        column: x => x.FeeAdjustmentRequestId,
                        principalTable: "Finance_FeeAdjustmentRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Finance_FeeAdjustmentTargetAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeAdjustmentRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    DueFeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeHeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<double>(type: "double precision", nullable: false),
                    ApplyBatchNumber = table.Column<int>(type: "integer", nullable: false),
                    AppliedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_FeeAdjustmentTargetAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finance_FeeAdjustmentTargetAllocations_Finance_FeeAdjustmen~",
                        column: x => x.FeeAdjustmentRequestId,
                        principalTable: "Finance_FeeAdjustmentRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeeAdjustmentRequests_Inbox",
                table: "Finance_FeeAdjustmentRequests",
                columns: new[] { "TenantId", "AcademicYearId", "BoardId", "EducationMediumId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_FeeAdjustmentRequests_Tenant_Voucher",
                table: "Finance_FeeAdjustmentRequests",
                columns: new[] { "TenantId", "VoucherNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeeAdjustmentSourceLines_DueFeeId",
                table: "Finance_FeeAdjustmentSourceLines",
                column: "DueFeeId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeAdjustmentSourceLines_RequestId",
                table: "Finance_FeeAdjustmentSourceLines",
                column: "FeeAdjustmentRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeAdjustmentTargetAllocations_DueFeeId",
                table: "Finance_FeeAdjustmentTargetAllocations",
                column: "DueFeeId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeAdjustmentTargetAllocations_RequestId",
                table: "Finance_FeeAdjustmentTargetAllocations",
                column: "FeeAdjustmentRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Finance_FeeAdjustmentSourceLines");

            migrationBuilder.DropTable(
                name: "Finance_FeeAdjustmentTargetAllocations");

            migrationBuilder.DropTable(
                name: "Finance_FeeAdjustmentRequests");

            migrationBuilder.DropColumn(
                name: "AdjustmentCreditAmount",
                table: "DueFees");
        }
    }
}
