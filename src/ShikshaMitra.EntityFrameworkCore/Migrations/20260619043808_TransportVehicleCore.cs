using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class TransportVehicleCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppTransportFuelTypes",
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
                    table.PrimaryKey("PK_AppTransportFuelTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppTransportRequiredVehicleDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaxSizeMb = table.Column<int>(type: "integer", nullable: false),
                    AcceptedFormatsCsv = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppTransportRequiredVehicleDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppTransportServiceTypes",
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
                    table.PrimaryKey("PK_AppTransportServiceTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppTransportVehicleCompliances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    InsuranceCompany = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PolicyNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InsuranceIssueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InsuranceExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CoverageAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PucCertificateNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PucIssueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PucExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FitnessCertificateNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FitnessIssueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FitnessExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RoadTaxReceiptNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RoadTaxIssueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_AppTransportVehicleCompliances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppTransportVehicleDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequiredDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    BlobName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
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
                    table.PrimaryKey("PK_AppTransportVehicleDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppTransportVehicleMaintenanceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ServiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextServiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PhotoBlobNames = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_AppTransportVehicleMaintenanceRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppTransportVehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegistrationNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    VehicleNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    VehicleName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    VehicleTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleCompany = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    YearOfManufacture = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    SeatingCapacity = table.Column<int>(type: "integer", nullable: false),
                    VehicleColor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SchoolAssignedNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Owner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FuelTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsAc = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppTransportVehicles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppTransportVehicleTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ApproxSittingRange = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("PK_AppTransportVehicleTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportFuelTypes_AcademicMediumId",
                table: "AppTransportFuelTypes",
                column: "AcademicMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportFuelTypes_AcademicMediumId_Name",
                table: "AppTransportFuelTypes",
                columns: new[] { "AcademicMediumId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportRequiredVehicleDocuments_AcademicMediumId",
                table: "AppTransportRequiredVehicleDocuments",
                column: "AcademicMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportRequiredVehicleDocuments_AcademicMediumId_Name",
                table: "AppTransportRequiredVehicleDocuments",
                columns: new[] { "AcademicMediumId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportServiceTypes_AcademicMediumId",
                table: "AppTransportServiceTypes",
                column: "AcademicMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportServiceTypes_AcademicMediumId_Name",
                table: "AppTransportServiceTypes",
                columns: new[] { "AcademicMediumId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportVehicleCompliances_AcademicMediumId",
                table: "AppTransportVehicleCompliances",
                column: "AcademicMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportVehicleCompliances_VehicleId",
                table: "AppTransportVehicleCompliances",
                column: "VehicleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportVehicleDocuments_AcademicMediumId",
                table: "AppTransportVehicleDocuments",
                column: "AcademicMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportVehicleDocuments_VehicleId",
                table: "AppTransportVehicleDocuments",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportVehicleDocuments_VehicleId_RequiredDocumentId",
                table: "AppTransportVehicleDocuments",
                columns: new[] { "VehicleId", "RequiredDocumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportVehicleMaintenanceRecords_AcademicMediumId",
                table: "AppTransportVehicleMaintenanceRecords",
                column: "AcademicMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportVehicleMaintenanceRecords_ServiceDate",
                table: "AppTransportVehicleMaintenanceRecords",
                column: "ServiceDate");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportVehicleMaintenanceRecords_VehicleId",
                table: "AppTransportVehicleMaintenanceRecords",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportVehicles_AcademicMediumId",
                table: "AppTransportVehicles",
                column: "AcademicMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportVehicles_AcademicMediumId_RegistrationNo",
                table: "AppTransportVehicles",
                columns: new[] { "AcademicMediumId", "RegistrationNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportVehicles_FuelTypeId",
                table: "AppTransportVehicles",
                column: "FuelTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportVehicles_VehicleTypeId",
                table: "AppTransportVehicles",
                column: "VehicleTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportVehicleTypes_AcademicMediumId",
                table: "AppTransportVehicleTypes",
                column: "AcademicMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportVehicleTypes_AcademicMediumId_Name",
                table: "AppTransportVehicleTypes",
                columns: new[] { "AcademicMediumId", "Name" },
                unique: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppTransportFuelTypes");

            migrationBuilder.DropTable(
                name: "AppTransportRequiredVehicleDocuments");

            migrationBuilder.DropTable(
                name: "AppTransportServiceTypes");

            migrationBuilder.DropTable(
                name: "AppTransportVehicleCompliances");

            migrationBuilder.DropTable(
                name: "AppTransportVehicleDocuments");

            migrationBuilder.DropTable(
                name: "AppTransportVehicleMaintenanceRecords");

            migrationBuilder.DropTable(
                name: "AppTransportVehicles");

            migrationBuilder.DropTable(
                name: "AppTransportVehicleTypes");

        }
    }
}
