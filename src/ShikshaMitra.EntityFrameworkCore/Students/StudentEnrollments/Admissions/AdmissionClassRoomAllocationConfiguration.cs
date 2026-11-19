using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class AdmissionClassRoomAllocationConfiguration : IEntityTypeConfiguration<AdmissionClassRoomAllocation>
{
	public void Configure(EntityTypeBuilder<AdmissionClassRoomAllocation> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionClassRoomAllocations", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(item => item.IsDeleted).IsRequired().ValueGeneratedNever().HasDefaultValue(false);
		builder.HasIndex(item => new { item.TenantId, item.AcademicYearId, item.StandardId, item.RoomId })
			.IsUnique().HasDatabaseName("UX_AdmissionClassRooms_ClassRoom").HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(item => new { item.TenantId, item.AcademicYearId, item.RoomId })
			.IsUnique().HasDatabaseName("UX_AdmissionClassRooms_CycleRoom").HasFilter("\"IsDeleted\" = false");
		builder.HasOne<AcademicYear>().WithMany().HasForeignKey(item => item.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<Standard>().WithMany().HasForeignKey(item => item.StandardId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<Room>().WithMany().HasForeignKey(item => item.RoomId).OnDelete(DeleteBehavior.Restrict);
	}
}
