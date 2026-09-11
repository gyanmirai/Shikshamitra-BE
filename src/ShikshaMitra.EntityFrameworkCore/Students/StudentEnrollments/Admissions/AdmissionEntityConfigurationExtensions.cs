using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

internal static class AdmissionEntityConfigurationExtensions
{
	internal static void ConfigureSoftDeleteDefault<TEntity>(this EntityTypeBuilder<TEntity> builder)
		where TEntity : class
	{
		builder.Property<bool>("IsDeleted")
			.IsRequired()
			.ValueGeneratedNever()
			.HasDefaultValue(false);
	}
}
