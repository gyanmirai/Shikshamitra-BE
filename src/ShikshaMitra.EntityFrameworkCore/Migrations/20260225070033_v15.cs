using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v15 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentFeeHeads");

            migrationBuilder.DropColumn(
                name: "StructureName",
                table: "StudentFeeStructures");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "ApproverId",
                table: "ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "ApproverMessage",
                table: "ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "RejectorId",
                table: "ConcessionRequests");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ConcessionRequests");

            migrationBuilder.RenameColumn(
                name: "AssignedAt",
                table: "StudentFeeStructures",
                newName: "AssignedOn");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcessionRequestConcessionTypeId",
                table: "ConcessionRequestFeeHeadValues",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ConcessionRequestConcessionTypeId",
                table: "ConcessionRequestFeeHeadMappings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ConcessionRequestConcessionTypeId",
                table: "ConcessionRequestDocuments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "ConcessionRequestConcessionTypes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApproverId",
                table: "ConcessionRequestConcessionTypes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApproverMessage",
                table: "ConcessionRequestConcessionTypes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreationTime",
                table: "ConcessionRequestConcessionTypes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatorId",
                table: "ConcessionRequestConcessionTypes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeleterId",
                table: "ConcessionRequestConcessionTypes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionTime",
                table: "ConcessionRequestConcessionTypes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ConcessionRequestConcessionTypes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModificationTime",
                table: "ConcessionRequestConcessionTypes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifierId",
                table: "ConcessionRequestConcessionTypes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "ConcessionRequestConcessionTypes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "ConcessionRequestConcessionTypes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectorId",
                table: "ConcessionRequestConcessionTypes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "ConcessionRequestConcessionTypes",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConcessionRequestConcessionTypeId",
                table: "ConcessionRequestFeeHeadValues");

            migrationBuilder.DropColumn(
                name: "ConcessionRequestConcessionTypeId",
                table: "ConcessionRequestFeeHeadMappings");

            migrationBuilder.DropColumn(
                name: "ConcessionRequestConcessionTypeId",
                table: "ConcessionRequestDocuments");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "ConcessionRequestConcessionTypes");

            migrationBuilder.DropColumn(
                name: "ApproverId",
                table: "ConcessionRequestConcessionTypes");

            migrationBuilder.DropColumn(
                name: "ApproverMessage",
                table: "ConcessionRequestConcessionTypes");

            migrationBuilder.DropColumn(
                name: "CreationTime",
                table: "ConcessionRequestConcessionTypes");

            migrationBuilder.DropColumn(
                name: "CreatorId",
                table: "ConcessionRequestConcessionTypes");

            migrationBuilder.DropColumn(
                name: "DeleterId",
                table: "ConcessionRequestConcessionTypes");

            migrationBuilder.DropColumn(
                name: "DeletionTime",
                table: "ConcessionRequestConcessionTypes");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ConcessionRequestConcessionTypes");

            migrationBuilder.DropColumn(
                name: "LastModificationTime",
                table: "ConcessionRequestConcessionTypes");

            migrationBuilder.DropColumn(
                name: "LastModifierId",
                table: "ConcessionRequestConcessionTypes");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "ConcessionRequestConcessionTypes");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "ConcessionRequestConcessionTypes");

            migrationBuilder.DropColumn(
                name: "RejectorId",
                table: "ConcessionRequestConcessionTypes");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ConcessionRequestConcessionTypes");

            migrationBuilder.RenameColumn(
                name: "AssignedOn",
                table: "StudentFeeStructures",
                newName: "AssignedAt");

            migrationBuilder.AddColumn<string>(
                name: "StructureName",
                table: "StudentFeeStructures",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "ConcessionRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApproverId",
                table: "ConcessionRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApproverMessage",
                table: "ConcessionRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "ConcessionRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "ConcessionRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectorId",
                table: "ConcessionRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "ConcessionRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "StudentFeeHeads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CategoryName = table.Column<string>(type: "text", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FeeHeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeHeadName = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsOneTime = table.Column<bool>(type: "boolean", nullable: false),
                    IsOptional = table.Column<bool>(type: "boolean", nullable: false),
                    IsSelected = table.Column<bool>(type: "boolean", nullable: false),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardFeeStructureId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentFeeStructureId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentFeeHeads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentFeeHeads_StudentFeeStructures_StudentFeeStructureId",
                        column: x => x.StudentFeeStructureId,
                        principalTable: "StudentFeeStructures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentFeeHeads_StudentFeeStructureId",
                table: "StudentFeeHeads",
                column: "StudentFeeStructureId");
        }
    }
}
