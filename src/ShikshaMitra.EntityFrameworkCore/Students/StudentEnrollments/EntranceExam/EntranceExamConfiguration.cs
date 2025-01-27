using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students.StudentEnrollments.EntranceExam;

public class EntranceExamConfiguration : IEntityTypeConfiguration<EntranceExam>
{
	public void Configure(EntityTypeBuilder<EntranceExam> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EntranceExams", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.HasOne(e => e.EntranceExamSlot)
			.WithMany()
			.HasForeignKey(e => e.EntranceExamSlotId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(e => e.StudentEnrollment)
			.WithMany()
			.HasForeignKey(e => e.StudentEnrollmentId)
			.OnDelete(DeleteBehavior.Restrict);

		// Auto Include 
		builder.Navigation(e => e.EntranceExamSlot).AutoInclude();
		builder.Navigation(e => e.StudentEnrollment).AutoInclude();
	}
}