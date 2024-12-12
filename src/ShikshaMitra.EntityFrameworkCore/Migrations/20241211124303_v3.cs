using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppPayrollStructures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EducationMediumStageId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayElementId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayrollStructureType = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<double>(type: "double precision", nullable: false),
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
                    table.PrimaryKey("PK_AppPayrollStructures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppPayrollStructures_AppEducationMediumStages_EducationMedi~",
                        column: x => x.EducationMediumStageId,
                        principalTable: "AppEducationMediumStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppPayrollStructures_AppPayElements_PayElementId",
                        column: x => x.PayElementId,
                        principalTable: "AppPayElements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppPayrollStructures_EducationMediumStageId",
                table: "AppPayrollStructures",
                column: "EducationMediumStageId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPayrollStructures_PayElementId",
                table: "AppPayrollStructures",
                column: "PayElementId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppPayrollStructures");
        }
    }
}
