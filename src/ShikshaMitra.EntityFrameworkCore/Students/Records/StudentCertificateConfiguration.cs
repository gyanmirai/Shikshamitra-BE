using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.Records;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students;

public class StudentCertificateConfiguration : IEntityTypeConfiguration<StudentCertificate>
{
	public void Configure(EntityTypeBuilder<StudentCertificate> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentCertificates", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.CertificateNumber).HasMaxLength(80).IsRequired();
		builder.Property(x => x.IdempotencyKey).HasMaxLength(100);
		builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");
		builder.Property(x => x.CertificateDataJson).HasColumnType("jsonb").IsRequired();
		builder.Property(x => x.TemplateSnapshotJson).HasColumnType("jsonb").IsRequired();

		builder.HasOne<Student>()
			.WithMany()
			.HasForeignKey(x => x.StudentId)
			.OnDelete(DeleteBehavior.Cascade)
			.IsRequired();

		builder.HasIndex(x => new { x.TenantId, x.CertificateType, x.CertificateNumber })
			.IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.StudentId, x.CreationTime });
	}
}
