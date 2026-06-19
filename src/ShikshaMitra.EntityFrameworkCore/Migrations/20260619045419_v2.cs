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
            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_AppAcademicYears_AcademicYearId",
                table: "AppStudents");

            migrationBuilder.DropTable(
                name: "FeeRefundLines");

            migrationBuilder.RenameIndex(
                name: "IX_Finance_StudentFeeStructures_StudentId",
                table: "Finance_StudentFeeStructures",
                newName: "IX_StudentFeeStructures_StudentId");

            migrationBuilder.CreateTable(
                name: "Finance_FeeRefundLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeRefundRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    DueFeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finance_FeeRefundLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finance_FeeRefundLines_Finance_FeeRefundRequests_FeeRefundR~",
                        column: x => x.FeeRefundRequestId,
                        principalTable: "Finance_FeeRefundRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Charges_CreatorId_Status",
                table: "Finance_Charges",
                columns: new[] { "CreatorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FeeRefundLines_DueFeeId",
                table: "Finance_FeeRefundLines",
                column: "DueFeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Finance_FeeRefundLines_FeeRefundRequestId",
                table: "Finance_FeeRefundLines",
                column: "FeeRefundRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_AppAcademicYears_AcademicYearId",
                table: "AppStudents",
                column: "AcademicYearId",
                principalTable: "AppAcademicYears",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_AppAcademicYears_AcademicYearId",
                table: "AppStudents");

            migrationBuilder.DropTable(
                name: "Finance_FeeRefundLines");

            migrationBuilder.DropIndex(
                name: "IX_Charges_CreatorId_Status",
                table: "Finance_Charges");

            migrationBuilder.RenameIndex(
                name: "IX_StudentFeeStructures_StudentId",
                table: "Finance_StudentFeeStructures",
                newName: "IX_Finance_StudentFeeStructures_StudentId");

            migrationBuilder.CreateTable(
                name: "FeeRefundLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<double>(type: "double precision", nullable: false),
                    DueFeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeRefundRequestId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeRefundLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeeRefundLines_Finance_FeeRefundRequests_FeeRefundRequestId",
                        column: x => x.FeeRefundRequestId,
                        principalTable: "Finance_FeeRefundRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeeRefundLines_DueFeeId",
                table: "FeeRefundLines",
                column: "DueFeeId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeRefundLines_FeeRefundRequestId",
                table: "FeeRefundLines",
                column: "FeeRefundRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_AppAcademicYears_AcademicYearId",
                table: "AppStudents",
                column: "AcademicYearId",
                principalTable: "AppAcademicYears",
                principalColumn: "Id");
        }
    }
}
