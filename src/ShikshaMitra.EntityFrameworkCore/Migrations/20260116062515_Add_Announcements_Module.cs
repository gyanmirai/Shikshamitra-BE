using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class Add_Announcements_Module : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAnnouncements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    AcademicMediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportanceLevel = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Duration = table.Column<int>(type: "integer", nullable: false),
                    DurationUnit = table.Column<int>(type: "integer", nullable: false),
                    Documents = table.Column<string>(type: "text", nullable: false),
                    Audience = table.Column<string>(type: "text", nullable: false),
                    NotificationPreferences = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppAnnouncements", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAnnouncements");
        }
    }
}
