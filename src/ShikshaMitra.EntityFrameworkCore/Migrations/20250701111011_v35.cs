using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
	/// <inheritdoc />
	public partial class v35 : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropForeignKey(
				name: "FK_AppSubjects_AppEducationMediums_AcademicMediumId",
				table: "AppSubjects"
			);

			migrationBuilder.DropColumn(
				name: "AcademicMediumId",
				table: "AppStandardSubjectAssignments"
			);

			migrationBuilder.DropColumn(
				name: "ScheduledDate",
				table: "AppStandardSubjectAssignments"
			);

			migrationBuilder.DropColumn(
				name: "SubjectColor",
				table: "AppStandardSubjectAssignments"
			);

			migrationBuilder.DropColumn(
				name: "SubjectName",
				table: "AppStandardSubjectAssignments"
			);

			migrationBuilder.RenameColumn(
				name: "AcademicMediumId",
				table: "AppSubjects",
				newName: "EducationMediumId"
			);

			migrationBuilder.RenameIndex(
				name: "IX_AppSubjects_AcademicMediumId",
				table: "AppSubjects",
				newName: "IX_AppSubjects_EducationMediumId"
			);

			migrationBuilder.AddColumn<Guid>(
				name: "ElectiveGroupId",
				table: "AppStandardSubjectAssignments",
				type: "uuid",
				nullable: true
			);

			migrationBuilder.CreateTable(
				name: "AppElectiveGroups",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "uuid", nullable: false),
					TenantId = table.Column<Guid>(type: "uuid", nullable: true),
					Name = table.Column<string>(type: "text", nullable: false),
					Color = table.Column<string>(type: "text", nullable: false),
					EducationMediumId = table.Column<Guid>(type: "uuid", nullable: false),
					ExtraProperties = table.Column<string>(type: "text", nullable: false),
					ConcurrencyStamp = table.Column<string>(
						type: "character varying(40)",
						maxLength: 40,
						nullable: false
					),
					CreationTime = table.Column<DateTime>(
						type: "timestamp with time zone",
						nullable: false
					),
					CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
					LastModificationTime = table.Column<DateTime>(
						type: "timestamp with time zone",
						nullable: true
					),
					LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
					IsDeleted = table.Column<bool>(
						type: "boolean",
						nullable: false,
						defaultValue: false
					),
					DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
					DeletionTime = table.Column<DateTime>(
						type: "timestamp with time zone",
						nullable: true
					),
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_AppElectiveGroups", x => x.Id);
					table.ForeignKey(
						name: "FK_AppElectiveGroups_AppEducationMediums_EducationMediumId",
						column: x => x.EducationMediumId,
						principalTable: "AppEducationMediums",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade
					);
				}
			);

			migrationBuilder.CreateTable(
				name: "AppSubjectChapters",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "uuid", nullable: false),
					TenantId = table.Column<Guid>(type: "uuid", nullable: true),
					StandardId = table.Column<Guid>(type: "uuid", nullable: false),
					SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
					ChapterNumber = table.Column<int>(type: "integer", nullable: false),
					ChapterName = table.Column<string>(type: "text", nullable: false),
					ChapterStartTime = table.Column<DateTimeOffset>(
						type: "timestamp with time zone",
						nullable: false
					),
					ChapterEndTime = table.Column<DateTimeOffset>(
						type: "timestamp with time zone",
						nullable: false
					),
					Description = table.Column<string>(type: "text", nullable: true),
					ExtraProperties = table.Column<string>(type: "text", nullable: false),
					ConcurrencyStamp = table.Column<string>(
						type: "character varying(40)",
						maxLength: 40,
						nullable: false
					),
					CreationTime = table.Column<DateTime>(
						type: "timestamp with time zone",
						nullable: false
					),
					CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
					LastModificationTime = table.Column<DateTime>(
						type: "timestamp with time zone",
						nullable: true
					),
					LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
					IsDeleted = table.Column<bool>(
						type: "boolean",
						nullable: false,
						defaultValue: false
					),
					DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
					DeletionTime = table.Column<DateTime>(
						type: "timestamp with time zone",
						nullable: true
					),
				},
				constraints: table =>
				{
					table.PrimaryKey("PK_AppSubjectChapters", x => x.Id);
				}
			);

			migrationBuilder.CreateTable(
				name: "AppElectiveGroupComponents",
				columns: table => new
				{
					ElectiveGroupId = table.Column<Guid>(type: "uuid", nullable: false),
					SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
				},
				constraints: table =>
				{
					table.PrimaryKey(
						"PK_AppElectiveGroupComponents",
						x => new { x.ElectiveGroupId, x.SubjectId }
					);
					table.ForeignKey(
						name: "FK_AppElectiveGroupComponents_AppElectiveGroups_ElectiveGroupId",
						column: x => x.ElectiveGroupId,
						principalTable: "AppElectiveGroups",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade
					);
					table.ForeignKey(
						name: "FK_AppElectiveGroupComponents_AppSubjects_SubjectId",
						column: x => x.SubjectId,
						principalTable: "AppSubjects",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade
					);
				}
			);

			migrationBuilder.CreateIndex(
				name: "IX_AppStandardSubjectAssignments_ElectiveGroupId",
				table: "AppStandardSubjectAssignments",
				column: "ElectiveGroupId"
			);

			migrationBuilder.CreateIndex(
				name: "IX_AppElectiveGroupComponents_SubjectId",
				table: "AppElectiveGroupComponents",
				column: "SubjectId"
			);

			migrationBuilder.CreateIndex(
				name: "IX_AppElectiveGroups_EducationMediumId",
				table: "AppElectiveGroups",
				column: "EducationMediumId"
			);

			migrationBuilder.AddForeignKey(
				name: "FK_AppStandardSubjectAssignments_AppElectiveGroups_ElectiveGro~",
				table: "AppStandardSubjectAssignments",
				column: "ElectiveGroupId",
				principalTable: "AppElectiveGroups",
				principalColumn: "Id"
			);

			migrationBuilder.AddForeignKey(
				name: "FK_AppSubjects_AppEducationMediums_EducationMediumId",
				table: "AppSubjects",
				column: "EducationMediumId",
				principalTable: "AppEducationMediums",
				principalColumn: "Id",
				onDelete: ReferentialAction.Cascade
			);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropForeignKey(
				name: "FK_AppStandardSubjectAssignments_AppElectiveGroups_ElectiveGro~",
				table: "AppStandardSubjectAssignments"
			);

			migrationBuilder.DropForeignKey(
				name: "FK_AppSubjects_AppEducationMediums_EducationMediumId",
				table: "AppSubjects"
			);

			migrationBuilder.DropTable(name: "AppElectiveGroupComponents");

			migrationBuilder.DropTable(name: "AppSubjectChapters");

			migrationBuilder.DropTable(name: "AppElectiveGroups");

			migrationBuilder.DropIndex(
				name: "IX_AppStandardSubjectAssignments_ElectiveGroupId",
				table: "AppStandardSubjectAssignments"
			);

			migrationBuilder.DropColumn(
				name: "ElectiveGroupId",
				table: "AppStandardSubjectAssignments"
			);

			migrationBuilder.RenameColumn(
				name: "EducationMediumId",
				table: "AppSubjects",
				newName: "AcademicMediumId"
			);

			migrationBuilder.RenameIndex(
				name: "IX_AppSubjects_EducationMediumId",
				table: "AppSubjects",
				newName: "IX_AppSubjects_AcademicMediumId"
			);

			migrationBuilder.AddColumn<Guid>(
				name: "AcademicMediumId",
				table: "AppStandardSubjectAssignments",
				type: "uuid",
				nullable: false,
				defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
			);

			migrationBuilder.AddColumn<DateTimeOffset>(
				name: "ScheduledDate",
				table: "AppStandardSubjectAssignments",
				type: "timestamp with time zone",
				nullable: false,
				defaultValue: new DateTimeOffset(
					new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
					new TimeSpan(0, 0, 0, 0, 0)
				)
			);

			migrationBuilder.AddColumn<string>(
				name: "SubjectColor",
				table: "AppStandardSubjectAssignments",
				type: "text",
				nullable: false,
				defaultValue: ""
			);

			migrationBuilder.AddColumn<string>(
				name: "SubjectName",
				table: "AppStandardSubjectAssignments",
				type: "text",
				nullable: false,
				defaultValue: ""
			);

			migrationBuilder.AddForeignKey(
				name: "FK_AppSubjects_AppEducationMediums_AcademicMediumId",
				table: "AppSubjects",
				column: "AcademicMediumId",
				principalTable: "AppEducationMediums",
				principalColumn: "Id",
				onDelete: ReferentialAction.Cascade
			);
		}
	}
}
