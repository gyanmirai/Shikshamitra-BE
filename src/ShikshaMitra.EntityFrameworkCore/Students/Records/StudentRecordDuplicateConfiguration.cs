using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.Records;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students;

public class StudentRecordDuplicateConfiguration : IEntityTypeConfiguration<StudentRecordDuplicateReview>
{
	public void Configure(EntityTypeBuilder<StudentRecordDuplicateReview> b)
	{
		b.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentRecordDuplicateReviews", ShikshaMitraConsts.DbSchema); b.ConfigureByConvention();
		b.Property(x => x.Reason).HasMaxLength(2000).IsRequired(); b.Property(x => x.ReviewReason).HasMaxLength(2000);
		b.Property(x => x.Status).HasMaxLength(30).IsRequired();
		b.HasIndex(x => new { x.TenantId, x.StudentId, x.OtherStudentId }).IsUnique().HasFilter("\"Status\" IN ('Open', 'ConfirmedDuplicate') AND \"IsDeleted\" = false");
		b.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
		b.HasOne<Student>().WithMany().HasForeignKey(x => x.OtherStudentId).OnDelete(DeleteBehavior.Restrict);
	}
}
