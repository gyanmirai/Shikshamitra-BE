using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class TransportStopRouteCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppTransportRoutes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RouteCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RouteType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Distance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Duration = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StartPoint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EndPoint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PickupStartTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PickupEndTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DropoffStartTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DropoffEndTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_AppTransportRoutes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppTransportStops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    StopName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Landmark = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AddressLine1 = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AddressLine2 = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PinCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ApproxKmFromSchool = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_AppTransportStops", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppTransportRouteStops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    StopId = table.Column<Guid>(type: "uuid", nullable: false),
                    StopSequence = table.Column<int>(type: "integer", nullable: false),
                    ExpectedArrivalTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpectedDepartureTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DistanceFromPrevious = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
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
                    table.PrimaryKey("PK_AppTransportRouteStops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppTransportRouteStops_AppTransportRoutes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "AppTransportRoutes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppTransportRouteStops_AppTransportStops_StopId",
                        column: x => x.StopId,
                        principalTable: "AppTransportStops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportRoutes_AcademicMediumId",
                table: "AppTransportRoutes",
                column: "AcademicMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportRoutes_AcademicMediumId_RouteCode",
                table: "AppTransportRoutes",
                columns: new[] { "AcademicMediumId", "RouteCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportRouteStops_RouteId",
                table: "AppTransportRouteStops",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportRouteStops_RouteId_StopId",
                table: "AppTransportRouteStops",
                columns: new[] { "RouteId", "StopId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportRouteStops_RouteId_StopSequence",
                table: "AppTransportRouteStops",
                columns: new[] { "RouteId", "StopSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportRouteStops_StopId",
                table: "AppTransportRouteStops",
                column: "StopId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportStops_AcademicMediumId",
                table: "AppTransportStops",
                column: "AcademicMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTransportStops_AcademicMediumId_StopName",
                table: "AppTransportStops",
                columns: new[] { "AcademicMediumId", "StopName" });

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppTransportRouteStops");

            migrationBuilder.DropTable(
                name: "AppTransportRoutes");

            migrationBuilder.DropTable(
                name: "AppTransportStops");
        }
    }
}
