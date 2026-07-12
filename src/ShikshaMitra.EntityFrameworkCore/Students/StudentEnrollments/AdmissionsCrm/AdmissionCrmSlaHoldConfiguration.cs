using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmSlaHoldConfiguration : IEntityTypeConfiguration<AdmissionCrmSlaHold>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmSlaHold> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmSlaHolds", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Reason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength).IsRequired();
		builder.Property(x => x.ResumeReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.HasIndex(x => new { x.TenantId, x.LeadId }).IsUnique().HasFilter("\"IsDeleted\" = false AND \"ResumedAt\" IS NULL");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.ResumedAt });
	}
}
