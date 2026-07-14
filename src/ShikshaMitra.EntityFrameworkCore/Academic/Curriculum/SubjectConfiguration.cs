using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Curriculum.Settings;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
	public void Configure(EntityTypeBuilder<Subject> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "Subjects", ShikshaMitraConsts.DbSchema);
		// Apply default configurations based on ABP conventions.
		builder.ConfigureByConvention();
		builder.Property(x => x.DisplayName).HasMaxLength(SubjectConsts.MaxDisplayNameLength);
		builder.Property(x => x.Category).HasMaxLength(SubjectConsts.MaxCategoryLength);
		builder.Property(x => x.Icon).HasMaxLength(SubjectConsts.MaxIconLength);
		builder.Property(x => x.Description).HasMaxLength(SubjectConsts.MaxDescriptionLength);
		builder.Property(x => x.Status).HasConversion<int>().HasDefaultValue(SubjectMasterStatus.Active);
	}
}
