using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class AdmissionReferenceSequenceConfiguration : IEntityTypeConfiguration<AdmissionReferenceSequence>
{
	public void Configure(EntityTypeBuilder<AdmissionReferenceSequence> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionReferenceSequences", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(item => item.StoredTenantId).IsRequired();
		builder.Property(item => item.Year).IsRequired();
		builder.Property(item => item.Kind).HasConversion<int>().IsRequired();
		builder.Property(item => item.NextValue).IsRequired().IsConcurrencyToken();
		builder.HasIndex(item => new { item.StoredTenantId, item.Year, item.Kind })
			.IsUnique()
			.HasDatabaseName("UX_AdmissionReferenceSequences_Tenant_Year_Kind");
	}
}
