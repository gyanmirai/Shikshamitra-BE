using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class AdmissionWorkflowConfiguration : IEntityTypeConfiguration<AdmissionWorkflowDefinition>, IEntityTypeConfiguration<AdmissionWorkflowStageDefinition>, IEntityTypeConfiguration<AdmissionWorkflowTransitionDefinition>
{
	public void Configure(EntityTypeBuilder<AdmissionWorkflowDefinition> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionWorkflowDefinitions", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Name).IsRequired().HasMaxLength(AdmissionWorkflowConsts.MaxNameLength);
		builder.Property(x => x.Code).IsRequired().HasMaxLength(AdmissionWorkflowConsts.MaxCodeLength);
		builder.HasIndex(x => new { x.TenantId, x.Code, x.Version }).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.Code, x.Status });
	}

	void IEntityTypeConfiguration<AdmissionWorkflowStageDefinition>.Configure(EntityTypeBuilder<AdmissionWorkflowStageDefinition> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionWorkflowStages", ShikshaMitraConsts.DbSchema);
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Code).IsRequired().HasMaxLength(AdmissionWorkflowConsts.MaxCodeLength);
		builder.Property(x => x.Name).IsRequired().HasMaxLength(AdmissionWorkflowConsts.MaxNameLength);
		builder.Property(x => x.Description).HasMaxLength(AdmissionWorkflowConsts.MaxStageDescriptionLength);
		builder.Property(x => x.ConfigurationJson).HasMaxLength(AdmissionWorkflowConsts.MaxConfigurationLength);
		builder.HasIndex(x => new { x.WorkflowDefinitionId, x.Code }).IsUnique();
	}

	void IEntityTypeConfiguration<AdmissionWorkflowTransitionDefinition>.Configure(EntityTypeBuilder<AdmissionWorkflowTransitionDefinition> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionWorkflowTransitions", ShikshaMitraConsts.DbSchema);
		builder.HasKey(x => x.Id);
		builder.HasIndex(x => new { x.WorkflowDefinitionId, x.FromStageId, x.ToStageId }).IsUnique();
	}
}
