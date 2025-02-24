using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students.StudentRecords;

public class StudentCertificateConfiguration : IEntityTypeConfiguration<StudentCertificate>
{
	public void Configure(EntityTypeBuilder<StudentCertificate> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentCertificates", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.HasOne(x => x.Student)
			.WithMany()
			.HasForeignKey(s => s.StudentId)
			.OnDelete(DeleteBehavior.Restrict);


		// Auto include Student
		builder.Navigation(x => x.Student).AutoInclude();
	}
}