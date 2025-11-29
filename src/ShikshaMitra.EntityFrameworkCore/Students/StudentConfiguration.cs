using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students;
public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
	public void Configure(EntityTypeBuilder<Student> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "Students", ShikshaMitraConsts.DbSchema);
		
		// Apply default configurations based on ABP conventions.
		builder.ConfigureByConvention();
		
		// Configure navigation properties to load aggregate parts together
		// This ensures the aggregate is loaded as a complete unit
		builder.HasOne(x => x.AdditionalDetail)
			.WithOne()
			.HasForeignKey<StudentAdditionalDetail>(x => x.Id)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.ContactDetail)
			.WithOne()
			.HasForeignKey<StudentContactDetail>(x => x.Id)
			.OnDelete(DeleteBehavior.Cascade);

		// Optional: Auto-include aggregate parts when loading Student
		// This follows the aggregate pattern - always load the complete aggregate
		builder.Navigation(x => x.AdditionalDetail).AutoInclude();
		builder.Navigation(x => x.ContactDetail).AutoInclude();
	}
}
