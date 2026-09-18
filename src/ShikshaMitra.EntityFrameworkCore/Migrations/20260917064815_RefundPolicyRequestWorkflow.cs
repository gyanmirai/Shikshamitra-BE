using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class RefundPolicyRequestWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "Finance_FeeRefundRequests",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AddColumn<decimal>(
                name: "ApprovedAmount",
                table: "Finance_FeeRefundRequests",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CalculatedAmount",
                table: "Finance_FeeRefundRequests",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CalculationHash",
                table: "Finance_FeeRefundRequests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CalculationSnapshotJson",
                table: "Finance_FeeRefundRequests",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Finance_FeeRefundRequests",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "Finance_FeeRefundRequests",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LegacyFinanciallyApplied",
                table: "Finance_FeeRefundRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalReceiptId",
                table: "Finance_FeeRefundRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalReceiptNumber",
                table: "Finance_FeeRefundRequests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicySnapshotJson",
                table: "Finance_FeeRefundRequests",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProcessedAmount",
                table: "Finance_FeeRefundRequests",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "RefundPolicyId",
                table: "Finance_FeeRefundRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RefundPolicyVersionId",
                table: "Finance_FeeRefundRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversalReason",
                table: "Finance_FeeRefundRequests",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReversedAmount",
                table: "Finance_FeeRefundRequests",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReversedAt",
                table: "Finance_FeeRefundRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversedByUserId",
                table: "Finance_FeeRefundRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewStartedAt",
                table: "Finance_FeeRefundRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByUserId",
                table: "Finance_FeeRefundRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StudentFeeStructureId",
                table: "Finance_FeeRefundRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedAt",
                table: "Finance_FeeRefundRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedByUserId",
                table: "Finance_FeeRefundRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Finance_FeeRefundLines",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AddColumn<decimal>(
                name: "ApprovedAmount",
                table: "Finance_FeeRefundLines",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CalculatedAmount",
                table: "Finance_FeeRefundLines",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "FeeHeadId",
                table: "Finance_FeeRefundLines",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "FeeHeadName",
                table: "Finance_FeeRefundLines",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InstallmentLabel",
                table: "Finance_FeeRefundLines",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ProcessedAmount",
                table: "Finance_FeeRefundLines",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReversedAmount",
                table: "Finance_FeeRefundLines",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SettledPaidAmount",
                table: "Finance_FeeRefundLines",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceAllocationId",
                table: "Finance_FeeRefundLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceReceiptId",
                table: "Finance_FeeRefundLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Finance_FeeRefundReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeRefundRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeRefundLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    DueFeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceAllocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_FeeRefundReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finance_FeeRefundReservations_Finance_FeeRefundRequests_Fee~",
                        column: x => x.FeeRefundRequestId,
                        principalTable: "Finance_FeeRefundRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Finance_FeeRefundSettlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeRefundRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    InitiatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Method = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SettlementDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PaymentDetailsJson = table.Column<string>(type: "jsonb", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReversalReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_FeeRefundSettlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finance_FeeRefundSettlements_Finance_FeeRefundRequests_FeeR~",
                        column: x => x.FeeRefundRequestId,
                        principalTable: "Finance_FeeRefundRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeRefundRequests_OriginalReceiptId",
                table: "Finance_FeeRefundRequests",
                column: "OriginalReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeRefundRequests_RefundPolicyVersionId",
                table: "Finance_FeeRefundRequests",
                column: "RefundPolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeRefundReservations_DueFeeId_Status",
                table: "Finance_FeeRefundReservations",
                columns: new[] { "DueFeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeRefundReservations_FeeRefundRequestId_FeeRefundL~",
                table: "Finance_FeeRefundReservations",
                columns: new[] { "FeeRefundRequestId", "FeeRefundLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeRefundSettlements_FeeRefundRequestId_Idempotency~",
                table: "Finance_FeeRefundSettlements",
                columns: new[] { "FeeRefundRequestId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Finance_FeeRefundReservations");

            migrationBuilder.DropTable(
                name: "Finance_FeeRefundSettlements");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeRefundRequests_OriginalReceiptId",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeRefundRequests_RefundPolicyVersionId",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "ApprovedAmount",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "CalculatedAmount",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "CalculationHash",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "CalculationSnapshotJson",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "LegacyFinanciallyApplied",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "OriginalReceiptId",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "OriginalReceiptNumber",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "PolicySnapshotJson",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "ProcessedAmount",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "RefundPolicyId",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "RefundPolicyVersionId",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "ReversalReason",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "ReversedAmount",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "ReversedAt",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "ReversedByUserId",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "ReviewStartedAt",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "StudentFeeStructureId",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "SubmittedByUserId",
                table: "Finance_FeeRefundRequests");

            migrationBuilder.DropColumn(
                name: "ApprovedAmount",
                table: "Finance_FeeRefundLines");

            migrationBuilder.DropColumn(
                name: "CalculatedAmount",
                table: "Finance_FeeRefundLines");

            migrationBuilder.DropColumn(
                name: "FeeHeadId",
                table: "Finance_FeeRefundLines");

            migrationBuilder.DropColumn(
                name: "FeeHeadName",
                table: "Finance_FeeRefundLines");

            migrationBuilder.DropColumn(
                name: "InstallmentLabel",
                table: "Finance_FeeRefundLines");

            migrationBuilder.DropColumn(
                name: "ProcessedAmount",
                table: "Finance_FeeRefundLines");

            migrationBuilder.DropColumn(
                name: "ReversedAmount",
                table: "Finance_FeeRefundLines");

            migrationBuilder.DropColumn(
                name: "SettledPaidAmount",
                table: "Finance_FeeRefundLines");

            migrationBuilder.DropColumn(
                name: "SourceAllocationId",
                table: "Finance_FeeRefundLines");

            migrationBuilder.DropColumn(
                name: "SourceReceiptId",
                table: "Finance_FeeRefundLines");

            migrationBuilder.AlterColumn<double>(
                name: "TotalAmount",
                table: "Finance_FeeRefundRequests",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<double>(
                name: "Amount",
                table: "Finance_FeeRefundLines",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");
        }
    }
}
