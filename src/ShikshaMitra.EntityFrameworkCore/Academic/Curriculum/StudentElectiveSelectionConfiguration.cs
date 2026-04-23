using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Curriculum;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Academic.Curriculum;

public class StudentElectiveSelectionConfiguration : IEntityTypeConfiguration<StudentElectiveSelection>
{
	public void Configure(EntityTypeBuilder<StudentElectiveSelection> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentElectiveSelections", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.StudentId).IsRequired();
		builder.Property(x => x.ElectiveGroupId).IsRequired();
		builder.Property(x => x.SelectedSubjectId).IsRequired();
		builder.Property(x => x.AcademicMediumId).IsRequired();
		builder.Property(x => x.StudentPreference).HasMaxLength(512);

		builder.HasIndex(x => new { x.StudentId, x.ElectiveGroupId }).IsUnique();
	}
}
