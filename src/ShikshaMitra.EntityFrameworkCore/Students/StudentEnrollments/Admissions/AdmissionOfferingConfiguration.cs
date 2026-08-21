using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class AdmissionOfferingConfiguration : IEntityTypeConfiguration<AdmissionOffering>
{
	public void Configure(EntityTypeBuilder<AdmissionOffering> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionOfferings", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Code).IsRequired().HasMaxLength(AdmissionOfferingConsts.MaxCodeLength);
		builder.Property(x => x.OpenDate).IsRequired(false);
		builder.Property(x => x.CloseDate).IsRequired(false);
		builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
		builder.Property(x => x.IsDeleted).IsRequired().ValueGeneratedNever().HasDefaultValue(false);

		builder.HasIndex(x => new { x.TenantId, x.AdmissionCycleId, x.BoardId, x.MediumId, x.StageId, x.StandardId })
			.IsUnique()
			.HasDatabaseName("UX_AdmissionOfferings_Identity_NoCampus")
			.HasFilter("\"IsDeleted\" = false AND \"CampusId\" IS NULL");
		builder.HasIndex(x => new { x.TenantId, x.AdmissionCycleId, x.CampusId, x.BoardId, x.MediumId, x.StageId, x.StandardId })
			.IsUnique()
			.HasDatabaseName("UX_AdmissionOfferings_Identity_Campus")
			.HasFilter("\"IsDeleted\" = false AND \"CampusId\" IS NOT NULL");
		builder.HasIndex(x => new { x.TenantId, x.AdmissionCycleId, x.IsActive, x.StandardId })
			.HasDatabaseName("IX_AdmissionOfferings_CycleActiveStandard");

		builder.HasOne<AdmissionCycle>().WithMany().HasForeignKey(x => x.AdmissionCycleId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<AdmissionCampus>().WithMany().HasForeignKey(x => x.CampusId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<EducationBoard>().WithMany().HasForeignKey(x => x.BoardId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<EducationMedium>().WithMany().HasForeignKey(x => x.MediumId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<EducationMediumStage>().WithMany().HasForeignKey(x => x.StageId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<Standard>().WithMany().HasForeignKey(x => x.StandardId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<AdmissionWorkflowDefinition>().WithMany().HasForeignKey(x => x.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<AdmissionFormDefinition>().WithMany().HasForeignKey(x => x.InquiryFormDefinitionId).OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<AdmissionFormDefinition>().WithMany().HasForeignKey(x => x.ApplicationFormDefinitionId).OnDelete(DeleteBehavior.Restrict);
	}
}
