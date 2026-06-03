using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddConcessionDueFeeAllocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ConcessionRequests_ConcessionTypeId",
                table: "AppConcessionRequests");

            migrationBuilder.DropIndex(
                name: "IX_ConcessionRequests_StudentId",
                table: "AppConcessionRequests");

            migrationBuilder.DropIndex(
                name: "IX_ConcessionRequests_StudentId_Status",
                table: "AppConcessionRequests");

            migrationBuilder.AlterColumn<decimal>(
                name: "UserSpecifiedConcessionAmountValue",
                table: "AppConcessionFeeHeadMappings",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "PreConcessionFeeHeadAmount",
                table: "AppConcessionFeeHeadMappings",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "PostConcessionFeeHeadAmount",
                table: "AppConcessionFeeHeadMappings",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "FeeHeadConcessionAmount",
                table: "AppConcessionFeeHeadMappings",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.CreateTable(
                name: "AppConcessionDueFeeAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConcessionRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    DueFeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeHeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcessionAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_AppConcessionDueFeeAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppConcessionDueFeeAllocations_AppConcessionRequests_Conces~",
                        column: x => x.ConcessionRequestId,
                        principalTable: "AppConcessionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionDueFeeAllocations_ConcessionRequestId",
                table: "AppConcessionDueFeeAllocations",
                column: "ConcessionRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionDueFeeAllocations_DueFeeId",
                table: "AppConcessionDueFeeAllocations",
                column: "DueFeeId");

            migrationBuilder.CreateIndex(
                name: "UIX_ConcessionDueFeeAllocations_Request_DueFee",
                table: "AppConcessionDueFeeAllocations",
                columns: new[] { "ConcessionRequestId", "DueFeeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppConcessionDueFeeAllocations");

            migrationBuilder.AlterColumn<decimal>(
                name: "UserSpecifiedConcessionAmountValue",
                table: "AppConcessionFeeHeadMappings",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "PreConcessionFeeHeadAmount",
                table: "AppConcessionFeeHeadMappings",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "PostConcessionFeeHeadAmount",
                table: "AppConcessionFeeHeadMappings",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "FeeHeadConcessionAmount",
                table: "AppConcessionFeeHeadMappings",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionRequests_ConcessionTypeId",
                table: "AppConcessionRequests",
                column: "ConcessionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionRequests_StudentId",
                table: "AppConcessionRequests",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ConcessionRequests_StudentId_Status",
                table: "AppConcessionRequests",
                columns: new[] { "StudentId", "Status" });
        }
    }
}
