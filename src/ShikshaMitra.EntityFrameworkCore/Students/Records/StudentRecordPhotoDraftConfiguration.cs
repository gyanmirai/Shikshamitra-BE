using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.Records;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students;

public class StudentRecordPhotoDraftConfiguration : IEntityTypeConfiguration<StudentRecordPhotoDraft>
{
	public void Configure(EntityTypeBuilder<StudentRecordPhotoDraft> b)
	{
		b.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentRecordPhotoDrafts", ShikshaMitraConsts.DbSchema);
		b.ConfigureByConvention();
		b.Property(x => x.BlobName).HasMaxLength(512).IsRequired();
		b.Property(x => x.FileName).HasMaxLength(255).IsRequired();
		b.Property(x => x.ContentType).HasMaxLength(80).IsRequired();
		b.HasIndex(x => new { x.TenantId, x.StudentId, x.UploadedBy });
		b.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
	}
}
