using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260925090000_AddAdmissionClassRoomAllocations")]
public partial class AddAdmissionClassRoomAllocations : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable(
			name: "AppAdmissionClassRoomAllocations",
			columns: table => new
			{
				Id = table.Column<Guid>(type: "uuid", nullable: false),
				TenantId = table.Column<Guid>(type: "uuid", nullable: true),
				AcademicYearId = table.Column<Guid>(type: "uuid", nullable: false),
				StandardId = table.Column<Guid>(type: "uuid", nullable: false),
				RoomId = table.Column<Guid>(type: "uuid", nullable: false),
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
				table.PrimaryKey("PK_AppAdmissionClassRoomAllocations", item => item.Id);
				table.ForeignKey("FK_AppAdmissionClassRoomAllocations_academic_Years_AcademicYearId",
					item => item.AcademicYearId, "academic_Years", "Id", onDelete: ReferentialAction.Restrict);
				table.ForeignKey("FK_AppAdmissionClassRoomAllocations_academic_Standards_StandardId",
					item => item.StandardId, "academic_Standards", "Id", onDelete: ReferentialAction.Restrict);
				table.ForeignKey("FK_AppAdmissionClassRoomAllocations_academic_Rooms_RoomId",
					item => item.RoomId, "academic_Rooms", "Id", onDelete: ReferentialAction.Restrict);
			});
		migrationBuilder.CreateIndex("IX_AppAdmissionClassRoomAllocations_AcademicYearId",
			"AppAdmissionClassRoomAllocations", "AcademicYearId");
		migrationBuilder.CreateIndex("IX_AppAdmissionClassRoomAllocations_StandardId",
			"AppAdmissionClassRoomAllocations", "StandardId");
		migrationBuilder.CreateIndex("IX_AppAdmissionClassRoomAllocations_RoomId",
			"AppAdmissionClassRoomAllocations", "RoomId");
		migrationBuilder.CreateIndex("UX_AdmissionClassRooms_ClassRoom", "AppAdmissionClassRoomAllocations",
			new[] { "TenantId", "AcademicYearId", "StandardId", "RoomId" }, unique: true,
			filter: "\"IsDeleted\" = false");
		migrationBuilder.CreateIndex("UX_AdmissionClassRooms_CycleRoom", "AppAdmissionClassRoomAllocations",
			new[] { "TenantId", "AcademicYearId", "RoomId" }, unique: true,
			filter: "\"IsDeleted\" = false");
	}

	protected override void Down(MigrationBuilder migrationBuilder) =>
		migrationBuilder.DropTable("AppAdmissionClassRoomAllocations");
}
