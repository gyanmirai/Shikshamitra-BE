using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CollectionPolicyId",
                table: "Finance_FeeStructures",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "Finance_DueFees",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "Finance_CollectionPolicies",
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
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EnableLateFee = table.Column<bool>(type: "boolean", nullable: false),
                    GracePeriodDays = table.Column<int>(type: "integer", nullable: false),
                    GracePeriodType = table.Column<int>(type: "integer", nullable: false),
                    LateFeeApplyOnFeeHeadsJson = table.Column<string>(type: "text", nullable: false),
                    LateFeeType = table.Column<int>(type: "integer", nullable: false),
                    FlatAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Percentage = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    PercentageBase = table.Column<int>(type: "integer", nullable: false),
                    AmountPerDay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AmountPerWeek = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AmountPerMonth = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CompoundFrequency = table.Column<int>(type: "integer", nullable: false),
                    EscalationRulesJson = table.Column<string>(type: "text", nullable: false),
                    FeeHeadOverridesJson = table.Column<string>(type: "text", nullable: false),
                    RoundingMethod = table.Column<int>(type: "integer", nullable: false),
                    ConsiderAsIncome = table.Column<bool>(type: "boolean", nullable: false),
                    LateFeeIncomeHeadId = table.Column<Guid>(type: "uuid", nullable: true),
                    MaximumLateFeeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MaximumChargingDays = table.Column<int>(type: "integer", nullable: true),
                    EnableWaiver = table.Column<bool>(type: "boolean", nullable: false),
                    WaiverApplyOnFeeHeadsJson = table.Column<string>(type: "text", nullable: false),
                    MaximumWaiverAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MaximumWaiverPercentage = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    WaiverExpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovalRoleIdsJson = table.Column<string>(type: "text", nullable: false),
                    ApprovalThresholdAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AllowOnlineCollection = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_Finance_CollectionPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Finance_CollectionPolicyExceptionWindows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Effect = table.Column<int>(type: "integer", nullable: false),
                    ExtensionDays = table.Column<int>(type: "integer", nullable: true),
                    ReasonCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_Finance_CollectionPolicyExceptionWindows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Finance_LateFeeCollectionAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FeeRecordMetadataId = table.Column<Guid>(type: "uuid", nullable: false),
                    DueFeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<double>(type: "numeric(18,2)", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_LateFeeCollectionAllocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Finance_LateFeeWaiverApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DueFeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionPolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComputedLateFeeAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EffectiveAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AppliedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BulkWaiverRequestId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Finance_LateFeeWaiverApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Finance_PolicyVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PolicyType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    PolicyName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false),
                    ChangeAction = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChangeReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_PolicyVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Finance_StudentFeePolicyOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeStructureId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsLateFeeExempt = table.Column<bool>(type: "boolean", nullable: false),
                    CustomGracePeriodDays = table.Column<int>(type: "integer", nullable: true),
                    WaiverPreApproved = table.Column<bool>(type: "boolean", nullable: false),
                    WaiverPreApprovalReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_Finance_StudentFeePolicyOverrides", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_CollectionPolicies_Tenant_AcademicYear_Default",
                table: "Finance_CollectionPolicies",
                columns: new[] { "TenantId", "AcademicYearId", "IsDefault" },
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_CollectionPolicies_Tenant_AcademicYear_Name",
                table: "Finance_CollectionPolicies",
                columns: new[] { "TenantId", "AcademicYearId", "PolicyName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ExceptionWindows_Tenant_Scope_DateRange",
                table: "Finance_CollectionPolicyExceptionWindows",
                columns: new[] { "TenantId", "Scope", "ScopeId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LateFeeCollectionAllocations_DueFeeId",
                table: "Finance_LateFeeCollectionAllocations",
                column: "DueFeeId");

            migrationBuilder.CreateIndex(
                name: "IX_LateFeeCollectionAllocations_FeeRecordMetadataId",
                table: "Finance_LateFeeCollectionAllocations",
                column: "FeeRecordMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_LateFeeWaiverApplications_DueFeeId",
                table: "Finance_LateFeeWaiverApplications",
                column: "DueFeeId");

            migrationBuilder.CreateIndex(
                name: "IX_LateFeeWaiverApplications_Tenant_Status",
                table: "Finance_LateFeeWaiverApplications",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_PolicyVersions_Tenant_Type_AcademicYear",
                table: "Finance_PolicyVersions",
                columns: new[] { "TenantId", "PolicyType", "AcademicYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_PolicyVersions_Tenant_Type_Policy_Version",
                table: "Finance_PolicyVersions",
                columns: new[] { "TenantId", "PolicyType", "PolicyId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_StudentFeePolicyOverrides_Tenant_Student_FeeStructure",
                table: "Finance_StudentFeePolicyOverrides",
                columns: new[] { "TenantId", "StudentId", "FeeStructureId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Finance_CollectionPolicies");

            migrationBuilder.DropTable(
                name: "Finance_CollectionPolicyExceptionWindows");

            migrationBuilder.DropTable(
                name: "Finance_LateFeeCollectionAllocations");

            migrationBuilder.DropTable(
                name: "Finance_LateFeeWaiverApplications");

            migrationBuilder.DropTable(
                name: "Finance_PolicyVersions");

            migrationBuilder.DropTable(
                name: "Finance_StudentFeePolicyOverrides");

            migrationBuilder.DropColumn(
                name: "CollectionPolicyId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "Finance_DueFees");
        }
    }
}
