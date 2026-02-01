using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class Add_Group_Management : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SelectionMode = table.Column<int>(type: "integer", nullable: false),
                    SelectedAcademicIds = table.Column<string>(type: "jsonb", nullable: false),
                    RoleIds = table.Column<string>(type: "jsonb", nullable: false),
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
                    table.PrimaryKey("PK_AppGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppGroups_AppAcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AppAcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppGroupEmployeeRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Field = table.Column<int>(type: "integer", nullable: false),
                    Operator = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppGroupEmployeeRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppGroupEmployeeRules_AppGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "AppGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppGroupMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberType = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppGroupMembers_AppGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "AppGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppGroupStudentRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Field = table.Column<int>(type: "integer", nullable: false),
                    Operator = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppGroupStudentRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppGroupStudentRules_AppGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "AppGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "AppGroupEmployeeRules_GroupId",
                table: "AppGroupEmployeeRules",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "AppGroupMembers_GroupId",
                table: "AppGroupMembers",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "AppGroupMembers_Unique",
                table: "AppGroupMembers",
                columns: new[] { "GroupId", "MemberType", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "AppGroupMembers_UserId",
                table: "AppGroupMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "AppGroups_AcademicYearId_Tenant",
                table: "AppGroups",
                columns: new[] { "AcademicYearId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "AppGroups_Name_Tenant",
                table: "AppGroups",
                columns: new[] { "Name", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "AppGroups_SelectionMode",
                table: "AppGroups",
                column: "SelectionMode");

            migrationBuilder.CreateIndex(
                name: "AppGroups_Tenant",
                table: "AppGroups",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "AppGroupStudentRules_GroupId",
                table: "AppGroupStudentRules",
                column: "GroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppGroupEmployeeRules");

            migrationBuilder.DropTable(
                name: "AppGroupMembers");

            migrationBuilder.DropTable(
                name: "AppGroupStudentRules");

            migrationBuilder.DropTable(
                name: "AppGroups");
        }
    }
}
