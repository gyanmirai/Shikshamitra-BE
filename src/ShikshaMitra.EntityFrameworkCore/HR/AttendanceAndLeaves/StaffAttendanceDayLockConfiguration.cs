using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra;
using ShikshaMitra.HR.AttendanceAndLeaves;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.AttendanceAndLeaves;

public class StaffAttendanceDayLockConfiguration : IEntityTypeConfiguration<StaffAttendanceDayLock>
{
	public void Configure(EntityTypeBuilder<StaffAttendanceDayLock> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StaffAttendanceDayLocks", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.LockDate)
			.HasColumnType("date");

		builder.HasIndex(x => new { x.TenantId, x.EducationMediumStageId, x.LockDate })
			.IsUnique()
			.HasDatabaseName("IX_StaffAttendanceDayLocks_Tenant_Stage_Date");

		builder.HasIndex(x => x.EducationMediumStageId)
			.HasDatabaseName("IX_StaffAttendanceDayLocks_EducationMediumStageId");
	}
}