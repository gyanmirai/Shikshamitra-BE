using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Payroll.Templates;

public class SalaryStructureTemplateConfiguration : IEntityTypeConfiguration<SalaryStructureTemplate>
{
	public void Configure(EntityTypeBuilder<SalaryStructureTemplate> builder)
	{
		builder.ToTable("HR_SalaryStructureTemplates");
		builder.ConfigureByConvention();
		builder.Property(x => x.Name).IsRequired().HasMaxLength(160);
		builder.Property(x => x.Description).HasMaxLength(500);
		builder.HasIndex(x => new { x.TenantId, x.Name }).IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => x.TenantId).IsUnique().HasFilter("\"IsDeleted\" = false AND \"IsDefault\" = true");
	}
}

public class SalaryStructureTemplateVersionConfiguration : IEntityTypeConfiguration<SalaryStructureTemplateVersion>
{
	public void Configure(EntityTypeBuilder<SalaryStructureTemplateVersion> builder)
	{
		builder.ToTable("HR_SalaryStructureTemplateVersions");
		builder.ConfigureByConvention();
		builder.Property(x => x.RulesJson).IsRequired().HasColumnType("jsonb");
		builder.Property(x => x.DefaultSalaryBasis).IsRequired();
		builder.Property(x => x.DefaultProtectionPolicy).IsRequired();
		builder.Property(x => x.DefaultPfApplicable).IsRequired();
		builder.Property(x => x.SaveOperationKey).IsRequired().HasMaxLength(100);
		builder.HasOne<SalaryStructureTemplate>()
			.WithMany()
			.HasForeignKey(x => x.TemplateId)
			.OnDelete(DeleteBehavior.Restrict);
		builder.HasIndex(x => new { x.TenantId, x.TemplateId, x.VersionNumber }).IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.TemplateId, x.Status });
		builder.HasIndex(x => new { x.TenantId, x.SaveOperationKey }).IsUnique()
			.HasFilter("\"IsDeleted\" = false AND \"SaveOperationKey\" <> ''");
	}
}
