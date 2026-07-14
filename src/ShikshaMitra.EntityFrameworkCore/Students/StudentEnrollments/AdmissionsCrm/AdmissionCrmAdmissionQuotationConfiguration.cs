using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmAdmissionQuotationConfiguration : IEntityTypeConfiguration<AdmissionCrmAdmissionQuotation>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmAdmissionQuotation> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmAdmissionQuotations", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.QuotationNumber).HasMaxLength(AdmissionCrmConsts.MaxQuotationNumberLength).IsRequired();
		builder.Property(x => x.Currency).HasMaxLength(AdmissionCrmConsts.MaxCurrencyCodeLength).IsRequired();
		builder.Property(x => x.ComponentsSnapshotJson).HasMaxLength(AdmissionCrmConsts.MaxFinancialPolicyJsonLength).IsRequired();
		builder.Property(x => x.GrossAmount).HasPrecision(18, 2);
		builder.Property(x => x.ConcessionAmount).HasPrecision(18, 2);
		builder.Property(x => x.NetAmount).HasPrecision(18, 2);
		builder.Property(x => x.RequiredAmount).HasPrecision(18, 2);
		builder.Property(x => x.ConfirmedAmount).HasPrecision(18, 2);
		builder.Property(x => x.FinanceReference).HasMaxLength(AdmissionCrmConsts.MaxFinanceReferenceLength);
		builder.Property(x => x.FinanceStateSource).HasMaxLength(AdmissionCrmConsts.MaxNameLength);
		builder.Property(x => x.GateOverrideReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.Property(x => x.Status).HasConversion<int>().IsRequired();
		builder.Property(x => x.GateType).HasConversion<int>().IsRequired();
		builder.Property(x => x.GateMilestone).HasConversion<int>().IsRequired();
		builder.Property(x => x.FinanceState).HasConversion<int>().IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.QuotationNumber })
			.IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.LeadId, x.Version })
			.IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Status, x.FinanceState });
		builder.HasIndex(x => new { x.TenantId, x.ValidUntil, x.Status });
	}
}
