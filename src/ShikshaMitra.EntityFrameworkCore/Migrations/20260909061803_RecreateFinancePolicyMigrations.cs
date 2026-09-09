using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class RecreateFinancePolicyMigrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Finance_CollectionPolicies_Tenant_AcademicYear_Default",
                table: "Finance_CollectionPolicies");

            migrationBuilder.AddColumn<string>(
                name: "AssignmentRequestFingerprint",
                table: "Finance_StudentFeeStructures",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ChargeEndDate",
                table: "Finance_StudentFeeStructures",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "ChargePeriodOverrideReason",
                table: "Finance_StudentFeeStructures",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChargePeriodSource",
                table: "Finance_StudentFeeStructures",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ChargePeriodSourceReferenceId",
                table: "Finance_StudentFeeStructures",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ChargeStartDate",
                table: "Finance_StudentFeeStructures",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "CollectionPolicyId",
                table: "Finance_StudentFeeStructures",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CollectionPolicyVersionId",
                table: "Finance_StudentFeeStructures",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProrationPolicyId",
                table: "Finance_StudentFeeStructures",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProrationPolicyVersionId",
                table: "Finance_StudentFeeStructures",
                type: "uuid",
                nullable: true);

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
                name: "CollectionPolicyVersionId",
                table: "Finance_FeeStructures",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProrationPolicyId",
                table: "Finance_FeeStructures",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProrationPolicyVersionId",
                table: "Finance_FeeStructures",
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

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Finance_FeeStructureDetails",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AddColumn<DateTime>(
                name: "ProrationChargeEndDate",
                table: "Finance_DueFees",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProrationChargeStartDate",
                table: "Finance_DueFees",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProrationChargedDays",
                table: "Finance_DueFees",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProrationCoverageDays",
                table: "Finance_DueFees",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProrationEligibleDays",
                table: "Finance_DueFees",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProrationEligibleEndDate",
                table: "Finance_DueFees",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProrationEligibleStartDate",
                table: "Finance_DueFees",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProrationExplanation",
                table: "Finance_DueFees",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProrationFactor",
                table: "Finance_DueFees",
                type: "numeric(12,8)",
                precision: 12,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProrationPolicyId",
                table: "Finance_DueFees",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProrationPolicyVersionId",
                table: "Finance_DueFees",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProrationRoundingMode",
                table: "Finance_DueFees",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProrationRuleType",
                table: "Finance_DueFees",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ScheduledAmount",
                table: "Finance_DueFees",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "Finance_ConcessionPolicies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Finance_ProrationPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PolicyCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Finance_ProrationPolicies", x => x.Id);
                });

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
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "Finance_ProrationPolicyRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProrationPolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeHeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleType = table.Column<int>(type: "integer", nullable: false),
                    RoundingMode = table.Column<int>(type: "integer", nullable: false),
                    MinimumChargeDays = table.Column<int>(type: "integer", nullable: true),
                    CutoffDay = table.Column<int>(type: "integer", nullable: true),
                    AfterCutoffAction = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_ProrationPolicyRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finance_ProrationPolicyRules_Finance_ProrationPolicies_Pror~",
                        column: x => x.ProrationPolicyId,
                        principalTable: "Finance_ProrationPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentFeeStructures_CollectionPolicyId",
                table: "Finance_StudentFeeStructures",
                column: "CollectionPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentFeeStructures_CollectionPolicyVersionId",
                table: "Finance_StudentFeeStructures",
                column: "CollectionPolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentFeeStructures_ProrationPolicyId",
                table: "Finance_StudentFeeStructures",
                column: "ProrationPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentFeeStructures_ProrationPolicyVersionId",
                table: "Finance_StudentFeeStructures",
                column: "ProrationPolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentFeeStructures_RefundPolicyId",
                table: "Finance_StudentFeeStructures",
                column: "RefundPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentFeeStructures_RefundPolicyVersionId",
                table: "Finance_StudentFeeStructures",
                column: "RefundPolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentFeeStructures_Tenant_Student_Year",
                table: "Finance_StudentFeeStructures",
                columns: new[] { "TenantId", "StudentId", "AcademicYearId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeStructures_CollectionPolicyId",
                table: "Finance_FeeStructures",
                column: "CollectionPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeStructures_CollectionPolicyVersionId",
                table: "Finance_FeeStructures",
                column: "CollectionPolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeStructures_ProrationPolicyId",
                table: "Finance_FeeStructures",
                column: "ProrationPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeStructures_ProrationPolicyVersionId",
                table: "Finance_FeeStructures",
                column: "ProrationPolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeStructures_RefundPolicyId",
                table: "Finance_FeeStructures",
                column: "RefundPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeStructures_RefundPolicyVersionId",
                table: "Finance_FeeStructures",
                column: "RefundPolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_DueFees_ProrationPolicyId",
                table: "Finance_DueFees",
                column: "ProrationPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_DueFees_ProrationPolicyVersionId",
                table: "Finance_DueFees",
                column: "ProrationPolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ConcessionPolicies_ActiveDefault",
                table: "Finance_ConcessionPolicies",
                columns: new[] { "TenantId", "AcademicYearId", "IsDefault" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" = 1 AND \"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_CollectionPolicies_Tenant_AcademicYear_Default",
                table: "Finance_CollectionPolicies",
                columns: new[] { "TenantId", "AcademicYearId", "IsDefault" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" = 1 AND \"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ProrationPolicies_ActiveDefault",
                table: "Finance_ProrationPolicies",
                columns: new[] { "TenantId", "AcademicYearId", "IsDefault" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" = 1 AND \"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ProrationPolicies_Tenant_Year_Code",
                table: "Finance_ProrationPolicies",
                columns: new[] { "TenantId", "AcademicYearId", "PolicyCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ProrationPolicies_Tenant_Year_Name",
                table: "Finance_ProrationPolicies",
                columns: new[] { "TenantId", "AcademicYearId", "PolicyName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ProrationPolicyRules_Policy_FeeHead",
                table: "Finance_ProrationPolicyRules",
                columns: new[] { "ProrationPolicyId", "FeeHeadId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_RefundPolicies_ActiveDefault",
                table: "Finance_RefundPolicies",
                columns: new[] { "TenantId", "AcademicYearId", "IsDefault" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" = 1 AND \"IsDefault\" = true");

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
                name: "FK_Finance_DueFees_Finance_PolicyVersions_ProrationPolicyVersi~",
                table: "Finance_DueFees",
                column: "ProrationPolicyVersionId",
                principalTable: "Finance_PolicyVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_DueFees_Finance_ProrationPolicies_ProrationPolicyId",
                table: "Finance_DueFees",
                column: "ProrationPolicyId",
                principalTable: "Finance_ProrationPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeeStructures_Finance_CollectionPolicies_Collection~",
                table: "Finance_FeeStructures",
                column: "CollectionPolicyId",
                principalTable: "Finance_CollectionPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeeStructures_Finance_PolicyVersions_CollectionPoli~",
                table: "Finance_FeeStructures",
                column: "CollectionPolicyVersionId",
                principalTable: "Finance_PolicyVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeeStructures_Finance_PolicyVersions_ProrationPolic~",
                table: "Finance_FeeStructures",
                column: "ProrationPolicyVersionId",
                principalTable: "Finance_PolicyVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeeStructures_Finance_PolicyVersions_RefundPolicyVe~",
                table: "Finance_FeeStructures",
                column: "RefundPolicyVersionId",
                principalTable: "Finance_PolicyVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_FeeStructures_Finance_ProrationPolicies_ProrationPo~",
                table: "Finance_FeeStructures",
                column: "ProrationPolicyId",
                principalTable: "Finance_ProrationPolicies",
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
                name: "FK_Finance_StudentFeeStructures_Finance_CollectionPolicies_Col~",
                table: "Finance_StudentFeeStructures",
                column: "CollectionPolicyId",
                principalTable: "Finance_CollectionPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_StudentFeeStructures_Finance_PolicyVersions_Collect~",
                table: "Finance_StudentFeeStructures",
                column: "CollectionPolicyVersionId",
                principalTable: "Finance_PolicyVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Finance_StudentFeeStructures_Finance_PolicyVersions_Prorati~",
                table: "Finance_StudentFeeStructures",
                column: "ProrationPolicyVersionId",
                principalTable: "Finance_PolicyVersions",
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
                name: "FK_Finance_StudentFeeStructures_Finance_ProrationPolicies_Pror~",
                table: "Finance_StudentFeeStructures",
                column: "ProrationPolicyId",
                principalTable: "Finance_ProrationPolicies",
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
                name: "FK_Finance_DueFees_Finance_PolicyVersions_ProrationPolicyVersi~",
                table: "Finance_DueFees");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_DueFees_Finance_ProrationPolicies_ProrationPolicyId",
                table: "Finance_DueFees");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeStructures_Finance_CollectionPolicies_Collection~",
                table: "Finance_FeeStructures");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeStructures_Finance_PolicyVersions_CollectionPoli~",
                table: "Finance_FeeStructures");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeStructures_Finance_PolicyVersions_ProrationPolic~",
                table: "Finance_FeeStructures");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeStructures_Finance_PolicyVersions_RefundPolicyVe~",
                table: "Finance_FeeStructures");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeStructures_Finance_ProrationPolicies_ProrationPo~",
                table: "Finance_FeeStructures");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_FeeStructures_Finance_RefundPolicies_RefundPolicyId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_StudentFeeStructures_Finance_CollectionPolicies_Col~",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_StudentFeeStructures_Finance_PolicyVersions_Collect~",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_StudentFeeStructures_Finance_PolicyVersions_Prorati~",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_StudentFeeStructures_Finance_PolicyVersions_RefundP~",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_StudentFeeStructures_Finance_ProrationPolicies_Pror~",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropForeignKey(
                name: "FK_Finance_StudentFeeStructures_Finance_RefundPolicies_RefundP~",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropTable(
                name: "Finance_ProrationPolicyRules");

            migrationBuilder.DropTable(
                name: "Finance_RefundPolicies");

            migrationBuilder.DropTable(
                name: "Finance_ProrationPolicies");

            migrationBuilder.DropIndex(
                name: "IX_StudentFeeStructures_CollectionPolicyId",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_StudentFeeStructures_CollectionPolicyVersionId",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_StudentFeeStructures_ProrationPolicyId",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_StudentFeeStructures_ProrationPolicyVersionId",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_StudentFeeStructures_RefundPolicyId",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_StudentFeeStructures_RefundPolicyVersionId",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_StudentFeeStructures_Tenant_Student_Year",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeStructures_CollectionPolicyId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeStructures_CollectionPolicyVersionId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeStructures_ProrationPolicyId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeStructures_ProrationPolicyVersionId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeStructures_RefundPolicyId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeStructures_RefundPolicyVersionId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_DueFees_ProrationPolicyId",
                table: "Finance_DueFees");

            migrationBuilder.DropIndex(
                name: "IX_DueFees_ProrationPolicyVersionId",
                table: "Finance_DueFees");

            migrationBuilder.DropIndex(
                name: "IX_Finance_ConcessionPolicies_ActiveDefault",
                table: "Finance_ConcessionPolicies");

            migrationBuilder.DropIndex(
                name: "IX_Finance_CollectionPolicies_Tenant_AcademicYear_Default",
                table: "Finance_CollectionPolicies");

            migrationBuilder.DropColumn(
                name: "AssignmentRequestFingerprint",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropColumn(
                name: "ChargeEndDate",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropColumn(
                name: "ChargePeriodOverrideReason",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropColumn(
                name: "ChargePeriodSource",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropColumn(
                name: "ChargePeriodSourceReferenceId",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropColumn(
                name: "ChargeStartDate",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropColumn(
                name: "CollectionPolicyId",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropColumn(
                name: "CollectionPolicyVersionId",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropColumn(
                name: "ProrationPolicyId",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropColumn(
                name: "ProrationPolicyVersionId",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropColumn(
                name: "RefundPolicyId",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropColumn(
                name: "RefundPolicyVersionId",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropColumn(
                name: "CollectionPolicyVersionId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropColumn(
                name: "ProrationPolicyId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropColumn(
                name: "ProrationPolicyVersionId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropColumn(
                name: "RefundPolicyId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropColumn(
                name: "RefundPolicyVersionId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropColumn(
                name: "ProrationChargeEndDate",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "ProrationChargeStartDate",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "ProrationChargedDays",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "ProrationCoverageDays",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "ProrationEligibleDays",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "ProrationEligibleEndDate",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "ProrationEligibleStartDate",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "ProrationExplanation",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "ProrationFactor",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "ProrationPolicyId",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "ProrationPolicyVersionId",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "ProrationRoundingMode",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "ProrationRuleType",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "ScheduledAmount",
                table: "Finance_DueFees");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "Finance_ConcessionPolicies");

            migrationBuilder.AlterColumn<double>(
                name: "Amount",
                table: "Finance_FeeStructureDetails",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_CollectionPolicies_Tenant_AcademicYear_Default",
                table: "Finance_CollectionPolicies",
                columns: new[] { "TenantId", "AcademicYearId", "IsDefault" },
                filter: "\"IsDefault\" = true");
        }
    }
}
