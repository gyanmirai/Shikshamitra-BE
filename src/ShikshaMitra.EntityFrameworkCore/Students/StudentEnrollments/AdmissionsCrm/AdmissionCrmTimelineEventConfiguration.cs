using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmTimelineEventConfiguration : IEntityTypeConfiguration<AdmissionCrmTimelineEvent>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmTimelineEvent> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmTimelineEvents",
			ShikshaMitraConsts.DbSchema);

		builder.ConfigureByConvention();

		builder.Property(x => x.Title)
			.HasMaxLength(AdmissionCrmConsts.MaxTimelineTitleLength)
			.IsRequired();
		builder.Property(x => x.Details)
			.HasMaxLength(AdmissionCrmConsts.MaxTimelineDetailsLength);
		builder.Property(x => x.EventType)
			.HasConversion<int>()
			.IsRequired();
		builder.Property(x => x.OccurredAtUtc)
			.IsRequired();

		builder.HasIndex(x => new { x.TenantId, x.LeadId, x.OccurredAtUtc });
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.EventType });
	}
}
