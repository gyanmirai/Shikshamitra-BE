using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ShikshaMitraDbContext))]
    [Migration("20260619203000_TransportFeeBillingCore")]
    public partial class TransportFeeBillingCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppTransportFeeConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModelType = table.Column<int>(type: "integer", nullable: false),
                    FeeFrequency = table.Column<int>(type: "integer", nullable: false),
                    RatePerKm = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ChargeCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_AppTransportFeeConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppTransportFeeDues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargeCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberType = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    StopId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeModelType = table.Column<int>(type: "integer", nullable: false),
                    FeeFrequency = table.Column<int>(type: "integer", nullable: false),
                    PeriodFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FinanceChargeRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    FinanceChargeLineId = table.Column<Guid>(type: "uuid", nullable: true),
                    FinancePostedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MemberName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    MemberCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RouteName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    RouteCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StopName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
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
                    table.PrimaryKey("PK_AppTransportFeeDues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppTransportFeeRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PeriodFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GeneratedDueCount = table.Column<int>(type: "integer", nullable: false),
                    StudentDueCount = table.Column<int>(type: "integer", nullable: false),
                    EmployeeDueCount = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FinancePostedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinanceChargeRequestCount = table.Column<int>(type: "integer", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_AppTransportFeeRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppTransportZoneAreas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppTransportZoneAreas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportFeeConfigurations_AcademicMediumId",
                table: "AppTransportFeeConfigurations",
                column: "AcademicMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportFeeConfigurations_AcademicMediumId_IsActive",
                table: "AppTransportFeeConfigurations",
                columns: new[] { "AcademicMediumId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportFeeConfigurations_Medium_Model_IsActive",
                table: "AppTransportFeeConfigurations",
                columns: new[] { "AcademicMediumId", "ModelType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportFeeConfigurations_ChargeCategoryId",
                table: "AppTransportFeeConfigurations",
                column: "ChargeCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportFeeDues_AcademicMediumId",
                table: "AppTransportFeeDues",
                column: "AcademicMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportFeeDues_Medium_Year_Status",
                table: "AppTransportFeeDues",
                columns: new[] { "AcademicMediumId", "AcademicYearId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportFeeDues_Medium_Run",
                table: "AppTransportFeeDues",
                columns: new[] { "AcademicMediumId", "FeeRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportFeeDues_Medium_Member",
                table: "AppTransportFeeDues",
                columns: new[] { "AcademicMediumId", "MemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportFeeDues_Medium_MemberType",
                table: "AppTransportFeeDues",
                columns: new[] { "AcademicMediumId", "MemberType" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportFeeDues_Medium_Route",
                table: "AppTransportFeeDues",
                columns: new[] { "AcademicMediumId", "RouteId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportFeeDues_Medium_Stop",
                table: "AppTransportFeeDues",
                columns: new[] { "AcademicMediumId", "StopId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportFeeDues_BillingPeriodKey",
                table: "AppTransportFeeDues",
                columns: new[] { "AcademicMediumId", "AcademicYearId", "FeeConfigurationId", "MemberId", "AssignmentId", "PeriodFrom", "PeriodTo" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportFeeDues_FinanceChargeRequestId",
                table: "AppTransportFeeDues",
                column: "FinanceChargeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportFeeDues_FinanceChargeLineId",
                table: "AppTransportFeeDues",
                column: "FinanceChargeLineId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportFeeRuns_AcademicMediumId",
                table: "AppTransportFeeRuns",
                column: "AcademicMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportFeeRuns_AcademicMediumId_AcademicYearId_Status",
                table: "AppTransportFeeRuns",
                columns: new[] { "AcademicMediumId", "AcademicYearId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportFeeRuns_BillingPeriodKey",
                table: "AppTransportFeeRuns",
                columns: new[] { "AcademicMediumId", "FeeConfigurationId", "AcademicYearId", "PeriodFrom", "PeriodTo" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportZoneAreas_AcademicMediumId",
                table: "AppTransportZoneAreas",
                column: "AcademicMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportZoneAreas_AcademicMediumId_Name",
                table: "AppTransportZoneAreas",
                columns: new[] { "AcademicMediumId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportZoneAreas_AcademicMediumId_IsActive",
                table: "AppTransportZoneAreas",
                columns: new[] { "AcademicMediumId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppTransportFeeConfigurations");

            migrationBuilder.DropTable(
                name: "AppTransportFeeDues");

            migrationBuilder.DropTable(
                name: "AppTransportFeeRuns");

            migrationBuilder.DropTable(
                name: "AppTransportZoneAreas");
        }
    }
}
