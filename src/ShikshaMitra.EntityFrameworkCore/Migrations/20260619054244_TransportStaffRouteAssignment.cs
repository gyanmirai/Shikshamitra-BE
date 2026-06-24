using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class TransportStaffRouteAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppTransportRouteAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverStaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConductorStaffId = table.Column<Guid>(type: "uuid", nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_AppTransportRouteAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppTransportStaff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    LicenseNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LicenseExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExperienceYears = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppTransportStaff", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportRouteAssignments_AcademicMediumId",
                table: "AppTransportRouteAssignments",
                column: "AcademicMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportRouteAssignments_AcademicMediumId_ConductorStaf~",
                table: "AppTransportRouteAssignments",
                columns: new[] { "AcademicMediumId", "ConductorStaffId", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportRouteAssignments_AcademicMediumId_DriverStaffId~",
                table: "AppTransportRouteAssignments",
                columns: new[] { "AcademicMediumId", "DriverStaffId", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportRouteAssignments_AcademicMediumId_RouteId_Effec~",
                table: "AppTransportRouteAssignments",
                columns: new[] { "AcademicMediumId", "RouteId", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportRouteAssignments_AcademicMediumId_VehicleId_Eff~",
                table: "AppTransportRouteAssignments",
                columns: new[] { "AcademicMediumId", "VehicleId", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportRouteAssignments_ConductorStaffId",
                table: "AppTransportRouteAssignments",
                column: "ConductorStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportRouteAssignments_DriverStaffId",
                table: "AppTransportRouteAssignments",
                column: "DriverStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportRouteAssignments_RouteId",
                table: "AppTransportRouteAssignments",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportRouteAssignments_VehicleId",
                table: "AppTransportRouteAssignments",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportStaff_AcademicMediumId",
                table: "AppTransportStaff",
                column: "AcademicMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportStaff_AcademicMediumId_EmployeeId_Role",
                table: "AppTransportStaff",
                columns: new[] { "AcademicMediumId", "EmployeeId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportStaff_AcademicMediumId_Role_Status",
                table: "AppTransportStaff",
                columns: new[] { "AcademicMediumId", "Role", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportStaff_EmployeeId",
                table: "AppTransportStaff",
                column: "EmployeeId");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppTransportRouteAssignments");

            migrationBuilder.DropTable(
                name: "AppTransportStaff");
        }
    }
}
