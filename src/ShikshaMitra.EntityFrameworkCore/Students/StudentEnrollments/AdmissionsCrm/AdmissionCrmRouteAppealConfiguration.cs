using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRouteAppealConfiguration : IEntityTypeConfiguration<AdmissionCrmRouteAppeal>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmRouteAppeal> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmRouteAppeals", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Reason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength).IsRequired();
		builder.Property(x => x.EvidenceJson).HasColumnType("text").IsRequired();
		builder.Property(x => x.DecisionReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Status });
		builder.HasIndex(x => new { x.TenantId, x.LeadId });
		builder.HasIndex(x => new { x.TenantId, x.RouteAssignmentId, x.Status });
	}
}
