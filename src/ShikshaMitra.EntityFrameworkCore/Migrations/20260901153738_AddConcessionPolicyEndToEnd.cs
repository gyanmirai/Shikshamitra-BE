using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddConcessionPolicyEndToEnd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AcademicYearId",
                table: "Finance_StudentFeeStructures",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ConcessionPolicyId",
                table: "Finance_StudentFeeStructures",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ConcessionPolicyVersionId",
                table: "Finance_StudentFeeStructures",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ConcessionPolicyId",
                table: "Finance_FeeStructures",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcessionPolicyVersionId",
                table: "Finance_FeeStructures",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcessionPolicyId",
                table: "Finance_ConcessionRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ConcessionPolicyVersionId",
                table: "Finance_ConcessionRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "StudentFeeStructureId",
                table: "Finance_ConcessionRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Finance_ConcessionPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PolicyCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ValueSource = table.Column<int>(type: "integer", nullable: false),
                    CalculationMethod = table.Column<int>(type: "integer", nullable: false),
                    PercentageValue = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    FixedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MaximumPercentage = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    FeeHeadScope = table.Column<int>(type: "integer", nullable: false),
                    ApplicableFeeHeadIdsJson = table.Column<string>(type: "text", nullable: false),
                    ApplyOn = table.Column<int>(type: "integer", nullable: false),
                    RoundingMethod = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Finance_ConcessionPolicies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentFeeStructures_ConcessionPolicyId",
                table: "Finance_StudentFeeStructures",
                column: "ConcessionPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeStructures_ConcessionPolicyId",
                table: "Finance_FeeStructures",
                column: "ConcessionPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ConcessionRequests_StudentId_ConcessionPolicyId_Sta~",
                table: "Finance_ConcessionRequests",
                columns: new[] { "StudentId", "ConcessionPolicyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ConcessionPolicies_Tenant_Year_Code",
                table: "Finance_ConcessionPolicies",
                columns: new[] { "TenantId", "AcademicYearId", "PolicyCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_ConcessionPolicies_Tenant_Year_Name",
                table: "Finance_ConcessionPolicies",
                columns: new[] { "TenantId", "AcademicYearId", "PolicyName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Finance_ConcessionPolicies");

            migrationBuilder.DropIndex(
                name: "IX_StudentFeeStructures_ConcessionPolicyId",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_Finance_FeeStructures_ConcessionPolicyId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropIndex(
                name: "IX_Finance_ConcessionRequests_StudentId_ConcessionPolicyId_Sta~",
                table: "Finance_ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropColumn(
                name: "ConcessionPolicyId",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropColumn(
                name: "ConcessionPolicyVersionId",
                table: "Finance_StudentFeeStructures");

            migrationBuilder.DropColumn(
                name: "ConcessionPolicyId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropColumn(
                name: "ConcessionPolicyVersionId",
                table: "Finance_FeeStructures");

            migrationBuilder.DropColumn(
                name: "ConcessionPolicyId",
                table: "Finance_ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "ConcessionPolicyVersionId",
                table: "Finance_ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "StudentFeeStructureId",
                table: "Finance_ConcessionRequests");
        }
    }
}
