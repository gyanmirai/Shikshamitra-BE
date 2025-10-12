using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Curriculum;

public class ElectiveGroupComponentConfiguration : IEntityTypeConfiguration<ElectiveGroupComponent>
{
	public void Configure(EntityTypeBuilder<ElectiveGroupComponent> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "ElectiveGroupComponents",
			ShikshaMitraConsts.DbSchema
		);
		builder.ConfigureByConvention();

		// Define composite primary key
		builder.HasKey(x => new { x.ElectiveGroupId, x.SubjectId });

		// Configure foreign keys
		builder
			.HasOne(x => x.ElectiveGroup)
			.WithMany(x => x.Subjects)
			.HasForeignKey(x => x.ElectiveGroupId);

		builder.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId);

		builder.Navigation(x => x.Subject).AutoInclude();
	}
}