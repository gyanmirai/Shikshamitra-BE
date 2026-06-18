using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using Volo.Abp.EntityFrameworkCore.Modeling;

public class StudentFeeStructureConfiguration : IEntityTypeConfiguration<StudentFeeStructure>
{
	public void Configure(EntityTypeBuilder<StudentFeeStructure> builder)
	{
		builder.ToTable("Finance_StudentFeeStructures", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.HasIndex(x => x.StudentId)
			.HasDatabaseName("IX_StudentFeeStructures_StudentId");

		builder.HasIndex(x => new { x.StudentId, x.AcademicYearId })
			.HasDatabaseName("IX_StudentFeeStructures_StudentId_AcademicYearId");
	}
}
