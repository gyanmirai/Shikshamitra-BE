using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Curriculum;

public class ElectiveSubjectComponentConfiguration
	: IEntityTypeConfiguration<ElectiveSubjectComponent>
{
	public void Configure(EntityTypeBuilder<ElectiveSubjectComponent> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "ElectiveSubjectComponents",
			ShikshaMitraConsts.DbSchema
		);
		builder.ConfigureByConvention();

		// Define composite primary key
		builder.HasKey(x => new { x.ElectiveSubjectId, x.SubjectId });

		// Configure foreign keys
		builder
			.HasOne(x => x.ElectiveSubject)
			.WithMany(x => x.Subjects)
			.HasForeignKey(x => x.ElectiveSubjectId);

		builder.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId);

		builder.Navigation(x => x.Subject).AutoInclude();
	}
}
