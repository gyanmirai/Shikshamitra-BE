using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class AdmissionApplicationNumberingConfiguration : IEntityTypeConfiguration<AdmissionApplicationNumberingRule>, IEntityTypeConfiguration<AdmissionApplicationNumberCounter>
{
	public void Configure(EntityTypeBuilder<AdmissionApplicationNumberingRule> b)
	{
		b.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionApplicationNumberingRules", ShikshaMitraConsts.DbSchema);
		b.ConfigureByConvention();
		b.Property(x => x.Prefix).IsRequired().HasMaxLength(20);
		b.Property(x => x.Separator).IsRequired().HasMaxLength(1);
		b.Property(x => x.Suffix).IsRequired().HasMaxLength(20);
		b.HasIndex(x => new { x.StoredTenantId, x.Version }).IsUnique();
	}
	public void Configure(EntityTypeBuilder<AdmissionApplicationNumberCounter> b)
	{
		b.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionApplicationNumberCounters", ShikshaMitraConsts.DbSchema);
		b.ConfigureByConvention();
		b.Property(x => x.ScopeKey).IsRequired().HasMaxLength(48);
		b.Property(x => x.LastYearText).IsRequired().HasMaxLength(16);
		b.Property(x => x.NextValue).IsConcurrencyToken();
		b.HasIndex(x => new { x.StoredTenantId, x.ScopeKey }).IsUnique();
	}
}
