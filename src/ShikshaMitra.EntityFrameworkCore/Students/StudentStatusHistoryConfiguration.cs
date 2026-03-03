using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students;

public class StudentStatusHistoryConfiguration : IEntityTypeConfiguration<StudentStatusHistory>
{
	public void Configure(EntityTypeBuilder<StudentStatusHistory> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentStatusHistories", ShikshaMitraConsts.DbSchema);

		// Apply ABP creation-auditing columns (CreationTime, CreatorId).
		builder.ConfigureByConvention();

		builder.Property(x => x.Remarks)
			.HasMaxLength(StudentStatusHistoryConsts.MaxRemarksLength);

		builder.Property(x => x.FromStatus)
			.HasConversion<int>();

		builder.Property(x => x.ToStatus)
			.HasConversion<int>();

		// Querying history for a specific student is the primary access pattern.
		builder.HasIndex(x => x.StudentId);
	}
}
