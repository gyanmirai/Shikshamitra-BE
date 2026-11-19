using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20261012120000_AddStudentRecordCustomProfiles")]
public partial class AddStudentRecordCustomProfiles : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable("AppStudentRecordCustomProfiles", columns: table => new
		{
			Id = table.Column<Guid>("uuid", nullable: false), TenantId = table.Column<Guid>("uuid", nullable: true),
			StudentId = table.Column<Guid>("uuid", nullable: false), BaselineSubmissionId = table.Column<Guid>("uuid", nullable: false),
			SourcesJson = table.Column<string>("text", nullable: false), SchemaJson = table.Column<string>("text", nullable: false),
			ValuesJson = table.Column<string>("text", nullable: false), ExtraProperties = table.Column<string>("text", nullable: false),
			ConcurrencyStamp = table.Column<string>("character varying(40)", maxLength: 40, nullable: false),
			CreationTime = table.Column<DateTime>("timestamp with time zone", nullable: false), CreatorId = table.Column<Guid>("uuid", nullable: true),
			LastModificationTime = table.Column<DateTime>("timestamp with time zone", nullable: true), LastModifierId = table.Column<Guid>("uuid", nullable: true),
			IsDeleted = table.Column<bool>("boolean", nullable: false, defaultValue: false), DeleterId = table.Column<Guid>("uuid", nullable: true),
			DeletionTime = table.Column<DateTime>("timestamp with time zone", nullable: true)
		}, constraints: table =>
		{
			table.PrimaryKey("PK_AppStudentRecordCustomProfiles", x => x.Id);
			table.ForeignKey("FK_AppStudentRecordCustomProfiles_AppStudents_StudentId", x => x.StudentId, "AppStudents", "Id", onDelete: ReferentialAction.Restrict);
		});
		migrationBuilder.CreateIndex("IX_AppStudentRecordCustomProfiles_StudentId", "AppStudentRecordCustomProfiles", "StudentId");
		migrationBuilder.CreateIndex("IX_AppStudentRecordCustomProfiles_TenantId_StudentId", "AppStudentRecordCustomProfiles",
			new[] { "TenantId", "StudentId" }, unique: true, filter: "\"IsDeleted\" = false");
	}
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("""
			DO $records_custom_rollback$
			BEGIN
				IF EXISTS (SELECT 1 FROM "AppStudentRecordCustomProfiles") THEN
					RAISE EXCEPTION 'Student custom corrections exist. Retain audited current values with a forward fix.';
				END IF;
			END $records_custom_rollback$;
			""");
		migrationBuilder.DropTable("AppStudentRecordCustomProfiles");
	}
}
