using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra;
using ShikshaMitra.Finance.StudentFeeAssignment;
using Volo.Abp.EntityFrameworkCore.Modeling;

public class StudentFeeStructureConfiguration : IEntityTypeConfiguration<StudentFeeStructure>
{
	public void Configure(EntityTypeBuilder<StudentFeeStructure> builder)
	{
		builder.ToTable("StudentFeeStructures", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.HasIndex(x => x.StudentId);
	}
}
