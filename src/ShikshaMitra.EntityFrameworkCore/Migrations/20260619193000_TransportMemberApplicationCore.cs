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
    [Migration("20260619193000_TransportMemberApplicationCore")]
    public partial class TransportMemberApplicationCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppTransportApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MemberType = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedStopId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedRouteId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedStopId = table.Column<Guid>(type: "uuid", nullable: true),
                    PickupType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Landmark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AreaLocality = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AddressLine1 = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EstimatedFeeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ApplicantName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ApplicantCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ApplicantRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ApplicantClassName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ApplicantDivisionName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ApplicantPhoneNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ApplicantPhotoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StatusRemarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedById = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_AppTransportApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppTransportMemberRouteAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    StopId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FeeAmountSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_AppTransportMemberRouteAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppTransportMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberType = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentRouteId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentStopId = table.Column<Guid>(type: "uuid", nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    MemberCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ClassName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DivisionName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PhoneNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    PhotoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_AppTransportMembers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportApplications_AcademicMediumId",
                table: "AppTransportApplications",
                column: "AcademicMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportApplications_AcademicMediumId_ApplicationType",
                table: "AppTransportApplications",
                columns: new[] { "AcademicMediumId", "ApplicationType" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportApplications_AcademicMediumId_AssignedRouteId",
                table: "AppTransportApplications",
                columns: new[] { "AcademicMediumId", "AssignedRouteId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportApplications_AcademicMediumId_AssignedStopId",
                table: "AppTransportApplications",
                columns: new[] { "AcademicMediumId", "AssignedStopId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportApplications_AcademicMediumId_MemberType_Emplo~",
                table: "AppTransportApplications",
                columns: new[] { "AcademicMediumId", "MemberType", "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportApplications_AcademicMediumId_MemberType_Stude~",
                table: "AppTransportApplications",
                columns: new[] { "AcademicMediumId", "MemberType", "StudentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportApplications_AcademicMediumId_Status",
                table: "AppTransportApplications",
                columns: new[] { "AcademicMediumId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportMemberRouteAssignments_AcademicMediumId",
                table: "AppTransportMemberRouteAssignments",
                column: "AcademicMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportMemberRouteAssignments_AcademicMediumId_Appli~",
                table: "AppTransportMemberRouteAssignments",
                columns: new[] { "AcademicMediumId", "ApplicationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportMemberRouteAssignments_AcademicMediumId_Membe~",
                table: "AppTransportMemberRouteAssignments",
                columns: new[] { "AcademicMediumId", "MemberId", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportMemberRouteAssignments_AcademicMediumId_Route~",
                table: "AppTransportMemberRouteAssignments",
                columns: new[] { "AcademicMediumId", "RouteId", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportMemberRouteAssignments_AcademicMediumId_StopI~",
                table: "AppTransportMemberRouteAssignments",
                columns: new[] { "AcademicMediumId", "StopId", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportMembers_AcademicMediumId",
                table: "AppTransportMembers",
                column: "AcademicMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportMembers_AcademicMediumId_CurrentRouteId",
                table: "AppTransportMembers",
                columns: new[] { "AcademicMediumId", "CurrentRouteId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportMembers_AcademicMediumId_CurrentStopId",
                table: "AppTransportMembers",
                columns: new[] { "AcademicMediumId", "CurrentStopId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportMembers_AcademicMediumId_MemberType_EmployeeId",
                table: "AppTransportMembers",
                columns: new[] { "AcademicMediumId", "MemberType", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportMembers_AcademicMediumId_MemberType_StudentId",
                table: "AppTransportMembers",
                columns: new[] { "AcademicMediumId", "MemberType", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportMembers_AcademicMediumId_Status",
                table: "AppTransportMembers",
                columns: new[] { "AcademicMediumId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppTransportApplications");

            migrationBuilder.DropTable(
                name: "AppTransportMemberRouteAssignments");

            migrationBuilder.DropTable(
                name: "AppTransportMembers");
        }
    }
}
