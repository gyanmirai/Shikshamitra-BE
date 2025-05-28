using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v29 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAddNewBooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BookTitle = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BookAuthor = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ISBN = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Publisher = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    AvailableCopies = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Condition = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PicturePath = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EditionNotes = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
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
                    table.PrimaryKey("PK_AppAddNewBooks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppIssueBooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Standard = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Division = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GrNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StudentName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BookId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookTitle = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EditionYear = table.Column<int>(type: "integer", nullable: false),
                    Publisher = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IssueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HasPendingCharges = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppIssueBooks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppReturnBooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BookId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookTitle = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Condition = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FineAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    FineReason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ReceivedFineAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PaymentMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NotificationSentOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotificationStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_AppReturnBooks", x => x.Id);
                });

          migrationBuilder.CreateIndex(
                name: "IX_AppAddNewBooks_ISBN",
                table: "AppAddNewBooks",
                column: "ISBN",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAddNewBooks");

            migrationBuilder.DropTable(
                name: "AppIssueBooks");

            migrationBuilder.DropTable(
                name: "AppReturnBooks");
		}
    }
}
