using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.Records.Settings;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.Divisions;

namespace ShikshaMitra.Students.Records;

public class HouseManagementConfiguration : IEntityTypeConfiguration<StudentEnrollment>, IEntityTypeConfiguration<HouseSettings>, IEntityTypeConfiguration<Division>
{
	public void Configure(EntityTypeBuilder<StudentEnrollment> builder)
	{
		builder.HasIndex(x => new { x.TenantId, x.HouseId });
		builder.HasOne<HouseSettings>().WithMany().HasForeignKey(x => x.HouseId).OnDelete(DeleteBehavior.Restrict);
	}

	public void Configure(EntityTypeBuilder<HouseSettings> builder)
	{
		builder.HasOne<StudentEnrollment>().WithMany().HasForeignKey(x => x.LeaderEnrollmentId).OnDelete(DeleteBehavior.Restrict);
	}

	public void Configure(EntityTypeBuilder<Division> builder)
	{
		builder.HasOne<StudentEnrollment>().WithMany().HasForeignKey(x => x.LeaderEnrollmentId).OnDelete(DeleteBehavior.Restrict);
	}
}
