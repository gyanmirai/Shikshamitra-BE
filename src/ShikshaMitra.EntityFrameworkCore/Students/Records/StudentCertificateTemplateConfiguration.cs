using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.Records;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students;

public class StudentCertificateTemplateConfiguration : IEntityTypeConfiguration<StudentCertificateTemplate>
{
	public void Configure(EntityTypeBuilder<StudentCertificateTemplate> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentCertificateTemplates", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.HeaderContent).HasMaxLength(4000).IsRequired();
		builder.Property(x => x.FooterContent).HasMaxLength(4000).IsRequired();
		builder.Property(x => x.LogoUrl).HasMaxLength(512);
		builder.Property(x => x.SchoolAddress).HasMaxLength(512);
		builder.Property(x => x.PrincipalSignatureUrl).HasMaxLength(512);
		builder.Property(x => x.SchoolStampUrl).HasMaxLength(512);

		builder.HasIndex(x => new { x.TenantId, x.CertificateType, x.IsActive });
	}
}
