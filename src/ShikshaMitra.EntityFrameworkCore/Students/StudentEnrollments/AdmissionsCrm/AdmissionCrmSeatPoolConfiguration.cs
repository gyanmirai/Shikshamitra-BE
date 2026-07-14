using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmSeatPoolConfiguration : IEntityTypeConfiguration<AdmissionCrmSeatPool>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmSeatPool> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmSeatPools", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Code).HasMaxLength(AdmissionCrmConsts.MaxSeatPoolCodeLength).IsRequired();
		builder.Property(x => x.Name).HasMaxLength(AdmissionCrmConsts.MaxNameLength).IsRequired();
		builder.Property(x => x.RoundCode).HasMaxLength(AdmissionCrmConsts.MaxPlaybookCodeLength);
		builder.Property(x => x.Campus).HasMaxLength(AdmissionCrmConsts.MaxNameLength);
		builder.Property(x => x.DesiredClass).HasMaxLength(AdmissionCrmConsts.MaxNameLength);
		builder.Property(x => x.Board).HasMaxLength(AdmissionCrmConsts.MaxNameLength);
		builder.Property(x => x.Medium).HasMaxLength(AdmissionCrmConsts.MaxNameLength);
		builder.Property(x => x.ApplicantCategory).HasMaxLength(AdmissionCrmConsts.MaxNameLength);
		builder.Property(x => x.Status).HasConversion<int>().IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Code })
			.IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Status });
		builder.Ignore(x => x.OccupiedSeats);
		builder.Ignore(x => x.RemainingCapacity);
		builder.Ignore(x => x.IsOverCapacity);
	}
}
