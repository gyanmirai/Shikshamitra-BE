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

			migrationBuilder.DropIndex(
				name: "IX_AppSubjects_AcademicMediumId",
				table: "AppSubjects"
			);

			migrationBuilder.DropColumn(name: "AcademicMediumId", table: "AppSubjects");

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

			migrationBuilder.AddColumn<Guid>(
				name: "ElectiveSubjectId",
				table: "AppStandardSubjectAssignments",
				type: "uuid",
				nullable: true
			);

			migrationBuilder.CreateTable(
				name: "AppElectiveSubjects",
				columns: table => new
				{
					Id = table.Column<Guid>(type: "uuid", nullable: false),
					TenantId = table.Column<Guid>(type: "uuid", nullable: true),
					Name = table.Column<string>(type: "text", nullable: false),
					Color = table.Column<string>(type: "text", nullable: false),
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
					table.PrimaryKey("PK_AppElectiveSubjects", x => x.Id);
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
				name: "AppElectiveSubjectComponents",
				columns: table => new
				{
					ElectiveSubjectId = table.Column<Guid>(type: "uuid", nullable: false),
					SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
				},
				constraints: table =>
				{
					table.PrimaryKey(
						"PK_AppElectiveSubjectComponents",
						x => new { x.ElectiveSubjectId, x.SubjectId }
					);
					table.ForeignKey(
						name: "FK_AppElectiveSubjectComponents_AppElectiveSubjects_ElectiveSu~",
						column: x => x.ElectiveSubjectId,
						principalTable: "AppElectiveSubjects",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade
					);
					table.ForeignKey(
						name: "FK_AppElectiveSubjectComponents_AppSubjects_SubjectId",
						column: x => x.SubjectId,
						principalTable: "AppSubjects",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade
					);
				}
			);

			migrationBuilder.CreateIndex(
				name: "IX_AppStandardSubjectAssignments_ElectiveSubjectId",
				table: "AppStandardSubjectAssignments",
				column: "ElectiveSubjectId"
			);

			migrationBuilder.CreateIndex(
				name: "IX_AppElectiveSubjectComponents_SubjectId",
				table: "AppElectiveSubjectComponents",
				column: "SubjectId"
			);

			migrationBuilder.AddForeignKey(
				name: "FK_AppStandardSubjectAssignments_AppElectiveSubjects_ElectiveS~",
				table: "AppStandardSubjectAssignments",
				column: "ElectiveSubjectId",
				principalTable: "AppElectiveSubjects",
				principalColumn: "Id"
			);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropForeignKey(
				name: "FK_AppStandardSubjectAssignments_AppElectiveSubjects_ElectiveS~",
				table: "AppStandardSubjectAssignments"
			);

			migrationBuilder.DropTable(name: "AppElectiveSubjectComponents");

			migrationBuilder.DropTable(name: "AppSubjectChapters");

			migrationBuilder.DropTable(name: "AppElectiveSubjects");

			migrationBuilder.DropIndex(
				name: "IX_AppStandardSubjectAssignments_ElectiveSubjectId",
				table: "AppStandardSubjectAssignments"
			);

			migrationBuilder.DropColumn(
				name: "ElectiveSubjectId",
				table: "AppStandardSubjectAssignments"
			);

			migrationBuilder.AddColumn<Guid>(
				name: "AcademicMediumId",
				table: "AppSubjects",
				type: "uuid",
				nullable: false,
				defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
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

			migrationBuilder.CreateIndex(
				name: "IX_AppSubjects_AcademicMediumId",
				table: "AppSubjects",
				column: "AcademicMediumId"
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
