using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class ImplementRefundPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RefundPolicyId",
                table: "Finance_StudentFeeStructures",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RefundPolicyVersionId",
                table: "Finance_StudentFeeStructures",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RefundPolicyId",
                table: "Finance_FeeStructures",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RefundPolicyVersionId",
                table: "Finance_FeeStructures",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Finance_RefundPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PolicyCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    EligiblePaymentState = table.Column<int>(type: "integer", nullable: false),
                    FeeHeadScope = table.Column<int>(type: "integer", nullable: false),
                    ApplicableFeeHeadIdsJson = table.Column<string>(type: "text", nullable: false),
                    IncludePaidCharges = table.Column<bool>(type: "boolean", nullable: false),
                    IncludePaidLateFee = table.Column<bool>(type: "boolean", nullable: false),
                    RequestDeadlineDays = table.Column<int>(type: "integer", nullable: true),
                    AllowPartialRefund = table.Column<bool>(type: "boolean", nullable: false),
                    AmountMode = table.Column<int>(type: "integer", nullable: false),
                    PercentageValue = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    FixedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MaximumPerRequest = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MaximumPerStudentYear = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    RoundingMethod = table.Column<int>(type: "integer", nullable: false),
                    DeductionType = table.Column<int>(type: "integer", nullable: false),
                    DeductionValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    MaximumDeduction = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DeductionExplanation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AllowedPayoutMethodsJson = table.Column<string>(type: "text", nullable: false),
                    ProcessingTargetDays = table.Column<int>(type: "integer", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Finance_RefundPolicies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentFeeStructures_RefundPolicyId",
                table: "Finance_StudentFeeStructures",
                column: "RefundPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentFeeStructures_RefundPolicyVersionId",
                table: "Finance_StudentFeeStructures",
                column: "RefundPolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeStructures_RefundPolicyId",
                table: "Finance_FeeStructures",
                column: "RefundPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeStructures_RefundPolicyVersionId",
                table: "Finance_FeeStructures",
                column: "RefundPolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_RefundPolicies_Tenant_Year_Code",
                table: "Finance_RefundPolicies",
                columns: new[] { "TenantId", "AcademicYearId", "PolicyCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_RefundPolicies_Tenant_Year_Name",
                table: "Finance_RefundPolicies",
                columns: new[] { "TenantId", "AcademicYearId", "PolicyName" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeeStructures_Finance_PolicyVersions_RefundPolicyVe~",
                table: "Finance_FeeStructures",
                column: "RefundPolicyVersionId",
                principalTable: "Finance_PolicyVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeeStructures_Finance_RefundPolicies_RefundPolicyId",
                table: "Finance_FeeStructures",
                column: "RefundPolicyId",
                principalTable: "Finance_RefundPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_StudentFeeStructures_Finance_PolicyVersions_RefundP~",
                table: "Finance_StudentFeeStructures",
                column: "RefundPolicyVersionId",
                principalTable: "Finance_PolicyVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_StudentFeeStructures_Finance_RefundPolicies_RefundP~",
                table: "Finance_StudentFeeStructures",
                column: "RefundPolicyId",
                principalTable: "Finance_RefundPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeStructures_Finance_PolicyVersions_RefundPolicyVe~",
                table: "Finance_FeeStructures");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeStructures_Finance_RefundPolicies_RefundPolicyId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_StudentFeeStructures_Finance_PolicyVersions_RefundP~",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_StudentFeeStructures_Finance_RefundPolicies_RefundP~",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropTable(
                name: "Finance_RefundPolicies");

            migrationBuilder.DropIndex(
                name: "IX_StudentFeeStructures_RefundPolicyId",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_StudentFeeStructures_RefundPolicyVersionId",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeStructures_RefundPolicyId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeStructures_RefundPolicyVersionId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropColumn(
                name: "RefundPolicyId",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropColumn(
                name: "RefundPolicyVersionId",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropColumn(
                name: "RefundPolicyId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropColumn(
                name: "RefundPolicyVersionId",
                table: "Finance_FeeStructures");
        }
    }
}
