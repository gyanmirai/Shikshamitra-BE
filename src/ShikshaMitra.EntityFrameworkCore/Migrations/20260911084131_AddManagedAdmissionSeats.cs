using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddManagedAdmissionSeats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAdmissionSeatPools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdmissionOfferingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    ReservedCount = table.Column<int>(type: "integer", nullable: false),
                    ConfirmedCount = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppAdmissionSeatPools", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAdmissionSeatPools_AppAdmissionOfferings_AdmissionOfferi~",
                        column: x => x.AdmissionOfferingId,
                        principalTable: "AppAdmissionOfferings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppAdmissionSeatReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RenewalCount = table.Column<int>(type: "integer", nullable: false),
                    LastActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
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
                    table.PrimaryKey("PK_AppAdmissionSeatReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAdmissionSeatReservations_AppAdmissionApplicationStageIn~",
                        column: x => x.StageInstanceId,
                        principalTable: "AppAdmissionApplicationStageInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppAdmissionSeatReservations_AppAdmissionApplications_Appli~",
                        column: x => x.ApplicationId,
                        principalTable: "AppAdmissionApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppAdmissionSeatReservations_AppAdmissionSeatPools_PoolId",
                        column: x => x.PoolId,
                        principalTable: "AppAdmissionSeatPools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionSeatPools_AdmissionOfferingId",
                table: "AppAdmissionSeatPools",
                column: "AdmissionOfferingId");

            migrationBuilder.CreateIndex(
                name: "UX_AdmissionSeatPools_Offering",
                table: "AppAdmissionSeatPools",
                columns: new[] { "TenantId", "AdmissionOfferingId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionSeatReservations_ApplicationId",
                table: "AppAdmissionSeatReservations",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionSeatReservations_PoolId",
                table: "AppAdmissionSeatReservations",
                column: "PoolId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionSeatReservations_StageInstanceId",
                table: "AppAdmissionSeatReservations",
                column: "StageInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionSeatReservations_TenantId_PoolId_Status_Creatio~",
                table: "AppAdmissionSeatReservations",
                columns: new[] { "TenantId", "PoolId", "Status", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAdmissionSeatReservations_TenantId_PoolId_Status_Expires~",
                table: "AppAdmissionSeatReservations",
                columns: new[] { "TenantId", "PoolId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "UX_AdmissionSeatReservations_ApplicationStage",
                table: "AppAdmissionSeatReservations",
                columns: new[] { "TenantId", "ApplicationId", "StageInstanceId" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAdmissionSeatReservations");

            migrationBuilder.DropTable(
                name: "AppAdmissionSeatPools");
        }
    }
}
