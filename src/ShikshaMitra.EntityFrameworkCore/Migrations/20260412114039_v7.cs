using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppLectureSlotConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolHours = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PreviousStandardId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_AppLectureSlotConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppTimetables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
                    DivisionId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_AppTimetables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppWeeklyLectureSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StandardId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_AppWeeklyLectureSlots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppLectureSlotGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    LectureSlotConfigId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalSlots = table.Column<int>(type: "integer", nullable: false),
                    WeekRangeStart = table.Column<int>(type: "integer", nullable: false),
                    WeekRangeEnd = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppLectureSlotGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppLectureSlotGroups_AppLectureSlotConfigs_LectureSlotConfi~",
                        column: x => x.LectureSlotConfigId,
                        principalTable: "AppLectureSlotConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppTimetableSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TimetableId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    SlotType = table.Column<int>(type: "integer", nullable: false),
                    BreakType = table.Column<int>(type: "integer", nullable: true),
                    SubjectType = table.Column<int>(type: "integer", nullable: true),
                    ElectiveGroupId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppTimetableSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppTimetableSlots_AppTimetables_TimetableId",
                        column: x => x.TimetableId,
                        principalTable: "AppTimetables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppWeeklyLectureSlotSubjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    WeeklyLectureSlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ElectiveGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    WeeklyLectures = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppWeeklyLectureSlotSubjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppWeeklyLectureSlotSubjects_AppWeeklyLectureSlots_WeeklyLe~",
                        column: x => x.WeeklyLectureSlotId,
                        principalTable: "AppWeeklyLectureSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppLectureSlotTimeSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LectureSlotGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    SlotType = table.Column<int>(type: "integer", nullable: false),
                    BreakType = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLectureSlotTimeSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppLectureSlotTimeSlots_AppLectureSlotGroups_LectureSlotGro~",
                        column: x => x.LectureSlotGroupId,
                        principalTable: "AppLectureSlotGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppTimetableSlotSubjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TimetableSlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppTimetableSlotSubjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppTimetableSlotSubjects_AppTimetableSlots_TimetableSlotId",
                        column: x => x.TimetableSlotId,
                        principalTable: "AppTimetableSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProvidentFunds_TenantId_Name",
                table: "AppProvidentFunds",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppLectureSlotConfigs_StandardId",
                table: "AppLectureSlotConfigs",
                column: "StandardId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLectureSlotGroups_LectureSlotConfigId",
                table: "AppLectureSlotGroups",
                column: "LectureSlotConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLectureSlotTimeSlots_LectureSlotGroupId",
                table: "AppLectureSlotTimeSlots",
                column: "LectureSlotGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTimetables_StandardId_DivisionId",
                table: "AppTimetables",
                columns: new[] { "StandardId", "DivisionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppTimetableSlots_TimetableId",
                table: "AppTimetableSlots",
                column: "TimetableId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTimetableSlotSubjects_TimetableSlotId",
                table: "AppTimetableSlotSubjects",
                column: "TimetableSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_AppWeeklyLectureSlots_StandardId",
                table: "AppWeeklyLectureSlots",
                column: "StandardId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppWeeklyLectureSlotSubjects_WeeklyLectureSlotId",
                table: "AppWeeklyLectureSlotSubjects",
                column: "WeeklyLectureSlotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppLectureSlotTimeSlots");

            migrationBuilder.DropTable(
                name: "AppTimetableSlotSubjects");

            migrationBuilder.DropTable(
                name: "AppWeeklyLectureSlotSubjects");

            migrationBuilder.DropTable(
                name: "AppLectureSlotGroups");

            migrationBuilder.DropTable(
                name: "AppTimetableSlots");

            migrationBuilder.DropTable(
                name: "AppWeeklyLectureSlots");

            migrationBuilder.DropTable(
                name: "AppLectureSlotConfigs");

            migrationBuilder.DropTable(
                name: "AppTimetables");

            migrationBuilder.DropIndex(
                name: "IX_ProvidentFunds_TenantId_Name",
                table: "AppProvidentFunds");
        }
    }
}
