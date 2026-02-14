using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra;
using ShikshaMitra.Finance.StudentFeeAssignment;
using Volo.Abp.EntityFrameworkCore.Modeling;

public class StudentFeeHeadConfiguration : IEntityTypeConfiguration<StudentFeeHead>
{
	public void Configure(EntityTypeBuilder<StudentFeeHead> builder)
	{
		builder.ToTable("StudentFeeHeads", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.Amount).HasColumnType("decimal(18,2)");

		builder.HasIndex(x => x.StudentFeeStructureId);
	}
}
