using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmConcessionRequestConfiguration : IEntityTypeConfiguration<AdmissionCrmConcessionRequest>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmConcessionRequest> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmConcessionRequests", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.RequestedAmount).HasPrecision(18, 2);
		builder.Property(x => x.RequestedPercentage).HasPrecision(7, 4);
		builder.Property(x => x.ApprovedAmount).HasPrecision(18, 2);
		builder.Property(x => x.Reason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength).IsRequired();
		builder.Property(x => x.EvidenceReference).HasMaxLength(AdmissionCrmConsts.MaxFinanceReferenceLength);
		builder.Property(x => x.DecisionReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.Property(x => x.FinanceReference).HasMaxLength(AdmissionCrmConsts.MaxFinanceReferenceLength);
		builder.Property(x => x.Type).HasConversion<int>().IsRequired();
		builder.Property(x => x.Status).HasConversion<int>().IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Status });
		builder.HasIndex(x => new { x.TenantId, x.LeadId, x.Status });
	}
}
