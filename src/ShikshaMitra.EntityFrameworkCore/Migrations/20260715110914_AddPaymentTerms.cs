using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PaymentTermId",
                table: "Finance_FeeStructureDetails",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentTermVersionId",
                table: "Finance_FeeStructureDetails",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScheduleMode",
                table: "Finance_FeeStructureDetails",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CoverageEndDate",
                table: "Finance_FeeCollectionDueFeeAllocations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CoverageStartDate",
                table: "Finance_FeeCollectionDueFeeAllocations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "Finance_FeeCollectionDueFeeAllocations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeeHeadName",
                table: "Finance_FeeCollectionDueFeeAllocations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstallmentLabel",
                table: "Finance_FeeCollectionDueFeeAllocations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScheduleSource",
                table: "Finance_FeeCollectionDueFeeAllocations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Finance_FeeStructureDetailScheduleOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FeeStructureDetailId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourcePaymentTermInstallmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CoverageStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CoverageEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DemandDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AllocationType = table.Column<int>(type: "integer", nullable: true),
                    AllocationValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
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
                    table.PrimaryKey("PK_Finance_FeeStructureDetailScheduleOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finance_FeeStructureDetailScheduleOverrides_Finance_FeeStru~",
                        column: x => x.FeeStructureDetailId,
                        principalTable: "Finance_FeeStructureDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Finance_PaymentTerms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    HolidayShiftingMethod = table.Column<int>(type: "integer", nullable: false),
                    DemandGenerationRule = table.Column<int>(type: "integer", nullable: false),
                    DemandOffsetDays = table.Column<int>(type: "integer", nullable: true),
                    InitialDemandStatus = table.Column<int>(type: "integer", nullable: false),
                    CurrentVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchiveReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_Finance_PaymentTerms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Finance_PaymentTermVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentTermId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ChangeReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SnapshotHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_PaymentTermVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finance_PaymentTermVersions_Finance_PaymentTerms_PaymentTer~",
                        column: x => x.PaymentTermId,
                        principalTable: "Finance_PaymentTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Finance_PaymentTermInstallments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentTermVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CoverageStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CoverageEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DemandDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AllocationType = table.Column<int>(type: "integer", nullable: false),
                    AllocationValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AllowPartialOverride = table.Column<bool>(type: "boolean", nullable: true),
                    GracePeriodDaysOverride = table.Column<int>(type: "integer", nullable: true),
                    DemandEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ReminderEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ReminderDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_PaymentTermInstallments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finance_PaymentTermInstallments_Finance_PaymentTermVersions~",
                        column: x => x.PaymentTermVersionId,
                        principalTable: "Finance_PaymentTermVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeStructureDetailScheduleOverrides_FeeStructureDet~",
                table: "Finance_FeeStructureDetailScheduleOverrides",
                column: "FeeStructureDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeStructureDetailScheduleOverrides_TenantId_FeeStr~",
                table: "Finance_FeeStructureDetailScheduleOverrides",
                columns: new[] { "TenantId", "FeeStructureDetailId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_PaymentTermInstallments_PaymentTermVersionId_Sequen~",
                table: "Finance_PaymentTermInstallments",
                columns: new[] { "PaymentTermVersionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_PaymentTerms_TenantId_AcademicYearId_BoardId_IsDefa~",
                table: "Finance_PaymentTerms",
                columns: new[] { "TenantId", "AcademicYearId", "BoardId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_Finance_PaymentTerms_TenantId_AcademicYearId_Code",
                table: "Finance_PaymentTerms",
                columns: new[] { "TenantId", "AcademicYearId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finance_PaymentTermVersions_PaymentTermId",
                table: "Finance_PaymentTermVersions",
                column: "PaymentTermId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_PaymentTermVersions_TenantId_PaymentTermId_VersionN~",
                table: "Finance_PaymentTermVersions",
                columns: new[] { "TenantId", "PaymentTermId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Finance_FeeStructureDetailScheduleOverrides");

            migrationBuilder.DropTable(
                name: "Finance_PaymentTermInstallments");

            migrationBuilder.DropTable(
                name: "Finance_PaymentTermVersions");

            migrationBuilder.DropTable(
                name: "Finance_PaymentTerms");

            migrationBuilder.DropColumn(
                name: "PaymentTermId",
                table: "Finance_FeeStructureDetails");

            migrationBuilder.DropColumn(
                name: "PaymentTermVersionId",
                table: "Finance_FeeStructureDetails");

            migrationBuilder.DropColumn(
                name: "ScheduleMode",
                table: "Finance_FeeStructureDetails");

            migrationBuilder.DropColumn(
                name: "CoverageEndDate",
                table: "Finance_FeeCollectionDueFeeAllocations");

            migrationBuilder.DropColumn(
                name: "CoverageStartDate",
                table: "Finance_FeeCollectionDueFeeAllocations");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "Finance_FeeCollectionDueFeeAllocations");

            migrationBuilder.DropColumn(
                name: "FeeHeadName",
                table: "Finance_FeeCollectionDueFeeAllocations");

            migrationBuilder.DropColumn(
                name: "InstallmentLabel",
                table: "Finance_FeeCollectionDueFeeAllocations");

            migrationBuilder.DropColumn(
                name: "ScheduleSource",
                table: "Finance_FeeCollectionDueFeeAllocations");
        }
    }
}
