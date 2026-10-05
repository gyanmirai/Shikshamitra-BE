using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Payroll.Statutory;

public class StatutoryRuleVersionConfiguration : IEntityTypeConfiguration<StatutoryRuleVersion>
{
	public void Configure(EntityTypeBuilder<StatutoryRuleVersion> builder)
	{
		builder.Property(x => x.EmployeeShareDailyWageExemption).HasPrecision(18, 2).HasDefaultValue(176m);
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StatutoryRuleVersions", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.SchemeCode).IsRequired().HasMaxLength(20);
		builder.Property(x => x.JurisdictionCode).IsRequired().HasMaxLength(20);
		builder.Property(x => x.StandardWageCeiling).HasPrecision(18, 2);
		builder.Property(x => x.DisabilityWageCeiling).HasPrecision(18, 2);
		builder.Property(x => x.EmployeePercentage).HasPrecision(7, 4);
		builder.Property(x => x.EmployerPercentage).HasPrecision(7, 4);
		builder.Property(x => x.ExclusionCapPercentage).HasPrecision(7, 4);
		builder.Property(x => x.InKindCapPercentage).HasPrecision(7, 4);
		builder.Property(x => x.EffectiveFrom).HasColumnType("date");
		builder.Property(x => x.EffectiveTo).HasColumnType("date");
		builder.Property(x => x.SourceUrl).IsRequired().HasMaxLength(1000);
		builder.Property(x => x.SourceVerifiedOn).HasColumnType("date");
		builder.HasIndex(x => new { x.TenantId, x.SchemeCode, x.JurisdictionCode, x.Version }).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.SchemeCode, x.EffectiveFrom, x.IsActive });
	}
}

public class StatutoryEstablishmentConfiguration : IEntityTypeConfiguration<StatutoryEstablishment>
{
	public void Configure(EntityTypeBuilder<StatutoryEstablishment> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StatutoryEstablishments", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.SchemeCode).IsRequired().HasMaxLength(20);
		builder.Property(x => x.JurisdictionCode).IsRequired().HasMaxLength(20);
		builder.Property(x => x.RegistrationNumber).HasMaxLength(100);
		builder.Property(x => x.CoveredSince).HasColumnType("date");
		builder.Property(x => x.BenefitsActiveFrom).HasColumnType("date");
		builder.Property(x => x.EvidenceReference).HasMaxLength(1000);
		builder.HasIndex(x => new { x.TenantId, x.SchemeCode }).IsUnique().HasFilter("\"IsDeleted\" = false");
	}
}

public class EmployeeStatutoryEnrollmentConfiguration : IEntityTypeConfiguration<EmployeeStatutoryEnrollment>
{
	public void Configure(EntityTypeBuilder<EmployeeStatutoryEnrollment> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EmployeeStatutoryEnrollments", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.SchemeCode).IsRequired().HasMaxLength(20);
		builder.Property(x => x.InsuranceNumber).HasMaxLength(100);
		builder.Property(x => x.CoveredFrom).HasColumnType("date");
		builder.Property(x => x.CoveredTo).HasColumnType("date");
		builder.Property(x => x.ContributionPeriodEnd).HasColumnType("date");
		builder.Property(x => x.StatusReason).HasMaxLength(500);
		builder.Property(x => x.EmployerShareReliefFrom).HasColumnType("date");
		builder.Property(x => x.EmployerShareReliefTo).HasColumnType("date");
		builder.Property(x => x.DisabilityEvidenceReference).HasMaxLength(1000);
		builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.SchemeCode }).IsUnique().HasFilter("\"IsDeleted\" = false");
	}
}

public class PayrollComponentStatutoryTreatmentConfiguration : IEntityTypeConfiguration<PayrollComponentStatutoryTreatment>
{
	public void Configure(EntityTypeBuilder<PayrollComponentStatutoryTreatment> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "PayrollComponentStatutoryTreatments", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.SchemeCode).IsRequired().HasMaxLength(20);
		builder.Property(x => x.EffectiveFrom).HasColumnType("date");
		builder.Property(x => x.EffectiveTo).HasColumnType("date");
		builder.HasIndex(x => new { x.TenantId, x.ComponentId, x.SchemeCode, x.EffectiveFrom }).IsUnique();
	}
}

public class StatutoryOverrideConfiguration : IEntityTypeConfiguration<StatutoryOverride>
{
	public void Configure(EntityTypeBuilder<StatutoryOverride> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StatutoryOverrides", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.SchemeCode).IsRequired().HasMaxLength(20);
		builder.Property(x => x.Amount).HasPrecision(18, 2);
		builder.Property(x => x.Reason).IsRequired().HasMaxLength(500);
		builder.Property(x => x.EvidenceReference).HasMaxLength(1000);
		builder.Property(x => x.EffectiveFrom).HasColumnType("date");
		builder.Property(x => x.EffectiveTo).HasColumnType("date");
		builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.SchemeCode, x.EffectiveFrom, x.EffectiveTo });
	}
}
