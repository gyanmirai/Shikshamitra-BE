using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectTeacherConfiguration : IEntityTypeConfiguration<SubjectTeacher>
{
	public void Configure(EntityTypeBuilder<SubjectTeacher> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "SubjectTeacher", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(st => new { st.SubjectId, st.TeacherId});

		builder.HasOne(st => st.Subject)
			.WithMany(s => s.SecondaryTeachers)
			.HasForeignKey(st => st.SubjectId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}
