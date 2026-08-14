using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class AdmissionCycleConfiguration : IEntityTypeConfiguration<AdmissionCycle>
{
	public void Configure(EntityTypeBuilder<AdmissionCycle> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCycles", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Name).IsRequired().HasMaxLength(AdmissionCycleConsts.MaxNameLength);
		builder.Property(x => x.Code).IsRequired().HasMaxLength(AdmissionCycleConsts.MaxCodeLength);
		builder.Property(x => x.Status).IsRequired().HasConversion<int>();
		builder.Property(x => x.StartDate).IsRequired();
		builder.Property(x => x.EndDate).IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Status });
	}
}
