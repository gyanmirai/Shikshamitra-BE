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

		builder.Property(x => x.Description)
			.HasMaxLength(InquiryPreferenceConsts.MaxDescriptionLength);

		builder.Property(x => x.DeactivationReason)
			.HasMaxLength(InquiryPreferenceConsts.MaxDeactivationReasonLength);

		builder.Property(x => x.DisplayOrder)
			.HasDefaultValue(0)
			.IsRequired();

		builder.Property(x => x.IsActive)
			.HasDefaultValue(true)
			.IsRequired();

		// AcademicYearId is required
		builder.Property(x => x.AcademicYearId)
			.IsRequired();

		// Create unique index on PreferenceName per tenant and academic year
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.PreferenceName })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false");
	}
}
