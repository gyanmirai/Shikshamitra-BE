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
		// Map the inherited soft-delete property explicitly. This keeps EF from
		// treating IsDeleted as an uninitialized shadow value when a workflow is
		// inserted through a repository.
		builder.Property(x => x.IsDeleted)
			.IsRequired()
			.ValueGeneratedNever()
			.HasDefaultValue(false);
		builder.Property(x => x.Name).IsRequired().HasMaxLength(AdmissionWorkflowConsts.MaxNameLength);
		builder.Property(x => x.Code).IsRequired().HasMaxLength(AdmissionWorkflowConsts.MaxCodeLength);
		builder.Property(x => x.Description).HasMaxLength(AdmissionWorkflowConsts.MaxDescriptionLength);
		builder.Property(x => x.DefaultInquiryFormDefinitionId).HasColumnType("uuid");
		builder.Property(x => x.ChangeReason).HasMaxLength(AdmissionWorkflowConsts.MaxReasonLength);
		builder.HasIndex(x => new { x.TenantId, x.Code, x.Version })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.Code, x.Status });
		builder.HasIndex(x => new { x.TenantId, x.Status });
		builder.HasIndex(x => x.ParentVersionId);
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
		builder.HasIndex(x => new { x.WorkflowDefinitionId, x.IsEntryStage });
		builder.HasIndex(x => new { x.WorkflowDefinitionId, x.IsTerminalStage });
		builder.HasOne<AdmissionWorkflowDefinition>()
			.WithMany()
			.HasForeignKey(x => x.WorkflowDefinitionId)
			.OnDelete(DeleteBehavior.Cascade);
	}

	void IEntityTypeConfiguration<AdmissionWorkflowTransitionDefinition>.Configure(EntityTypeBuilder<AdmissionWorkflowTransitionDefinition> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionWorkflowTransitions", ShikshaMitraConsts.DbSchema);
		builder.HasKey(x => x.Id);
		builder.Property(x => x.ConditionRuleJson).HasMaxLength(AdmissionWorkflowConsts.MaxConditionRuleLength);
		builder.HasIndex(x => new { x.WorkflowDefinitionId, x.FromStageId, x.ToStageId })
			.HasDatabaseName("UX_AdmWfTransitions_Workflow_From_To")
			.IsUnique();
		builder.HasIndex(x => new { x.WorkflowDefinitionId, x.FromStageId, x.Priority })
			.HasDatabaseName("IX_AdmWfTransitions_Workflow_From_Priority");
		builder.HasIndex(x => new { x.WorkflowDefinitionId, x.FromStageId, x.IsDefault })
			.HasDatabaseName("UX_AdmWfTransitions_Workflow_From_Default")
			.IsUnique()
			.HasFilter("\"IsDefault\" = true");
		builder.HasOne<AdmissionWorkflowDefinition>()
			.WithMany()
			.HasForeignKey(x => x.WorkflowDefinitionId)
			.OnDelete(DeleteBehavior.Cascade);
		builder.HasOne<AdmissionWorkflowStageDefinition>()
			.WithMany()
			.HasForeignKey(x => x.FromStageId)
			.OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<AdmissionWorkflowStageDefinition>()
			.WithMany()
			.HasForeignKey(x => x.ToStageId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}

public class AdmissionWorkflowGovernanceSettingConfiguration : IEntityTypeConfiguration<AdmissionWorkflowGovernanceSetting>
{
	public void Configure(EntityTypeBuilder<AdmissionWorkflowGovernanceSetting> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionWorkflowGovernanceSettings", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasIndex(x => x.TenantId).IsUnique();
	}
}

public class AdmissionWorkflowGovernanceHistoryConfiguration : IEntityTypeConfiguration<AdmissionWorkflowGovernanceHistory>
{
	public void Configure(EntityTypeBuilder<AdmissionWorkflowGovernanceHistory> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionWorkflowGovernanceHistories", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Action).HasConversion<int>().IsRequired();
		builder.Property(x => x.FromStatus).HasConversion<int>();
		builder.Property(x => x.ToStatus).HasConversion<int>().IsRequired();
		builder.Property(x => x.Reason).HasMaxLength(AdmissionWorkflowConsts.MaxReasonLength);
		builder.HasIndex(x => new { x.TenantId, x.WorkflowDefinitionId, x.OccurredAt });
		builder.HasOne<AdmissionWorkflowDefinition>().WithMany().HasForeignKey(x => x.WorkflowDefinitionId).OnDelete(DeleteBehavior.Cascade);
	}
}

public class AdmissionWorkflowNotificationConfiguration : IEntityTypeConfiguration<AdmissionWorkflowNotification>
{
	public void Configure(EntityTypeBuilder<AdmissionWorkflowNotification> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionWorkflowNotifications", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Type).HasConversion<int>().IsRequired();
		builder.Property(x => x.Title).IsRequired().HasMaxLength(AdmissionWorkflowConsts.MaxNotificationTitleLength);
		builder.Property(x => x.Message).IsRequired().HasMaxLength(AdmissionWorkflowConsts.MaxNotificationMessageLength);
		builder.HasIndex(x => new { x.TenantId, x.RecipientUserId, x.IsRead, x.CreationTime });
		builder.HasOne<AdmissionWorkflowDefinition>().WithMany().HasForeignKey(x => x.WorkflowDefinitionId).OnDelete(DeleteBehavior.Cascade);
	}
}
