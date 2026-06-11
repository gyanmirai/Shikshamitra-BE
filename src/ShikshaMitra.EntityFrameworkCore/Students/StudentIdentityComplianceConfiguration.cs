using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.Compliance;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students;

public class StudentIdentityComplianceConfiguration : IEntityTypeConfiguration<StudentIdentityCompliance>
{
	public void Configure(EntityTypeBuilder<StudentIdentityCompliance> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentIdentityCompliances", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.AadhaarEncrypted).HasMaxLength(1024);
		builder.Property(x => x.AadhaarHash).HasMaxLength(128);
		builder.Property(x => x.AadhaarLast4).HasMaxLength(4);
		builder.Property(x => x.AadhaarName).HasMaxLength(200);
		builder.Property(x => x.ApaarId).HasMaxLength(20);
		builder.Property(x => x.PenNumber).HasMaxLength(20);
		builder.Property(x => x.UdiseStudentCode).HasMaxLength(50);
		builder.Property(x => x.ScholarshipSchemeName).HasMaxLength(200);
		builder.Property(x => x.ScholarshipApplicationNo).HasMaxLength(50);
		builder.Property(x => x.CasteCertificateNo).HasMaxLength(50);

		builder.HasOne<Student>()
			.WithOne()
			.HasForeignKey<StudentIdentityCompliance>(x => x.StudentId)
			.OnDelete(DeleteBehavior.Cascade)
			.IsRequired();

		builder.HasIndex(x => x.StudentId).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.AadhaarHash });
		builder.HasIndex(x => new { x.TenantId, x.ApaarId });
		builder.HasIndex(x => new { x.TenantId, x.PenNumber });
		builder.HasIndex(x => new { x.TenantId, x.UdiseSyncStatus });
	}
}
