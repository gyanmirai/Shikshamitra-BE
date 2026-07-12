using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmFormNumberSeriesConfiguration : IEntityTypeConfiguration<AdmissionCrmFormNumberSeries>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmFormNumberSeries> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmFormNumberSeries", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Name).HasMaxLength(AdmissionCrmConsts.MaxTemplateNameLength).IsRequired();
		builder.Property(x => x.Prefix).HasMaxLength(AdmissionCrmConsts.MaxFormNumberPatternLength).IsRequired();
		builder.Property(x => x.Suffix).HasMaxLength(AdmissionCrmConsts.MaxFormNumberPatternLength).IsRequired();
		builder.Property(x => x.ApplicabilityJson).HasMaxLength(AdmissionCrmConsts.MaxRegistrationTemplateJsonLength).IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Name })
			.IsUnique().HasFilter("\"IsDeleted\" = false");
	}
}
