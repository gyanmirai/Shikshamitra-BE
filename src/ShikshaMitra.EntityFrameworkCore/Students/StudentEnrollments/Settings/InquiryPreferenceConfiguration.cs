using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Settings;

/// <summary>
/// EF Core configuration for InquiryPreference entity
/// </summary>
public class InquiryPreferenceConfiguration : IEntityTypeConfiguration<InquiryPreference>
{
	public void Configure(EntityTypeBuilder<InquiryPreference> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "InquiryPreferences",
			ShikshaMitraConsts.DbSchema
		);

		builder.ConfigureByConvention();

		// Configure PreferenceName property
		builder.Property(x => x.PreferenceName)
			.HasMaxLength(InquiryPreferenceConsts.MaxPreferenceNameLength)
			.IsRequired();

		// Create unique index on PreferenceName per tenant
		builder.HasIndex(x => new { x.TenantId, x.PreferenceName })
			.IsUnique();
	}
}
