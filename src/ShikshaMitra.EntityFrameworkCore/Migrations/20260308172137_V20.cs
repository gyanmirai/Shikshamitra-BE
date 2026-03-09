using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class V20 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_AppEducationMediums_AcademicMediumId",
                table: "AppStudents");

            migrationBuilder.CreateTable(
                name: "AppBookIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BookCopyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessionNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberRole = table.Column<int>(type: "integer", nullable: false),
                    IssueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ChargeCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    FineDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBookIssues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppBookReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BookId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReserveDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBookReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppBookReservations_AppBooks_BookId",
                        column: x => x.BookId,
                        principalTable: "AppBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppLibraryCharges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberRole = table.Column<int>(type: "integer", nullable: false),
                    ChargeCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookIssueId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ChargeDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaymentStatus = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PaidDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryCharges", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_AcademicBoardId",
                table: "AppStudents",
                column: "AcademicBoardId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_DivisionId",
                table: "AppStudents",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudents_StandardId",
                table: "AppStudents",
                column: "StandardId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBookIssues_AccessionNumber",
                table: "AppBookIssues",
                column: "AccessionNumber");

            migrationBuilder.CreateIndex(
                name: "IX_AppBookIssues_BookCopyId",
                table: "AppBookIssues",
                column: "BookCopyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBookIssues_MemberId",
                table: "AppBookIssues",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBookReservations_BookId",
                table: "AppBookReservations",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBookReservations_MemberId",
                table: "AppBookReservations",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCharges_BookIssueId",
                table: "AppLibraryCharges",
                column: "BookIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCharges_ChargeCategoryId",
                table: "AppLibraryCharges",
                column: "ChargeCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryCharges_MemberId",
                table: "AppLibraryCharges",
                column: "MemberId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_AppDivisions_DivisionId",
                table: "AppStudents",
                column: "DivisionId",
                principalTable: "AppDivisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_AppEducationBoards_AcademicBoardId",
                table: "AppStudents",
                column: "AcademicBoardId",
                principalTable: "AppEducationBoards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_AppEducationMediums_AcademicMediumId",
                table: "AppStudents",
                column: "AcademicMediumId",
                principalTable: "AppEducationMediums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_AppStandards_StandardId",
                table: "AppStudents",
                column: "StandardId",
                principalTable: "AppStandards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_AppDivisions_DivisionId",
                table: "AppStudents");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_AppEducationBoards_AcademicBoardId",
                table: "AppStudents");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_AppEducationMediums_AcademicMediumId",
                table: "AppStudents");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStudents_AppStandards_StandardId",
                table: "AppStudents");

            migrationBuilder.DropTable(
                name: "AppBookIssues");

            migrationBuilder.DropTable(
                name: "AppBookReservations");

            migrationBuilder.DropTable(
                name: "AppLibraryCharges");

            migrationBuilder.DropIndex(
                name: "IX_AppStudents_AcademicBoardId",
                table: "AppStudents");

            migrationBuilder.DropIndex(
                name: "IX_AppStudents_DivisionId",
                table: "AppStudents");

            migrationBuilder.DropIndex(
                name: "IX_AppStudents_StandardId",
                table: "AppStudents");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudents_AppEducationMediums_AcademicMediumId",
                table: "AppStudents",
                column: "AcademicMediumId",
                principalTable: "AppEducationMediums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
