using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v34 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Fines_Members_MemberId",
                table: "Fines");

            migrationBuilder.DropIndex(
                name: "IX_Fines_MemberId",
                table: "Fines");

            migrationBuilder.DropColumn(
                name: "MemberId",
                table: "Fines");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Fines");

            migrationBuilder.DropColumn(
                name: "AccountHolderName",
                table: "BooksAssignments");

            migrationBuilder.DropColumn(
                name: "BankAccountNumber",
                table: "BooksAssignments");

            migrationBuilder.DropColumn(
                name: "FineAmount",
                table: "BooksAssignments");

            migrationBuilder.DropColumn(
                name: "IFSCCode",
                table: "BooksAssignments");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "BooksAssignments");

            migrationBuilder.DropColumn(
                name: "PaymentMode",
                table: "BooksAssignments");

            migrationBuilder.DropColumn(
                name: "PenaltyAmount",
                table: "BooksAssignments");

            migrationBuilder.DropColumn(
                name: "PenaltyReason",
                table: "BooksAssignments");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "BooksAssignments");

            migrationBuilder.RenameColumn(
                name: "TotalFineAmount",
                table: "Fines",
                newName: "PenaltyAmount");

            migrationBuilder.AddColumn<string>(
                name: "AccountHolderName",
                table: "Fines",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAccountNumber",
                table: "Fines",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FineAmount",
                table: "Fines",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "IFSCCode",
                table: "Fines",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PenaltyReason",
                table: "Fines",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountHolderName",
                table: "Fines");

            migrationBuilder.DropColumn(
                name: "BankAccountNumber",
                table: "Fines");

            migrationBuilder.DropColumn(
                name: "FineAmount",
                table: "Fines");

            migrationBuilder.DropColumn(
                name: "IFSCCode",
                table: "Fines");

            migrationBuilder.DropColumn(
                name: "PenaltyReason",
                table: "Fines");

            migrationBuilder.RenameColumn(
                name: "PenaltyAmount",
                table: "Fines",
                newName: "TotalFineAmount");

            migrationBuilder.AddColumn<Guid>(
                name: "MemberId",
                table: "Fines",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Fines",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AccountHolderName",
                table: "BooksAssignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankAccountNumber",
                table: "BooksAssignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FineAmount",
                table: "BooksAssignments",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "IFSCCode",
                table: "BooksAssignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "BooksAssignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentMode",
                table: "BooksAssignments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PenaltyAmount",
                table: "BooksAssignments",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PenaltyReason",
                table: "BooksAssignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "BooksAssignments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Fines_MemberId",
                table: "Fines",
                column: "MemberId");

            migrationBuilder.AddForeignKey(
                name: "FK_Fines_Members_MemberId",
                table: "Fines",
                column: "MemberId",
                principalTable: "Members",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
