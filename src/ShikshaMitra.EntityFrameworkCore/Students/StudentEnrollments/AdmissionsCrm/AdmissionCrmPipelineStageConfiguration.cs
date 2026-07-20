using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPipelineStageConfiguration : IEntityTypeConfiguration<AdmissionCrmPipelineStage>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmPipelineStage> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmPipelineStages",
			ShikshaMitraConsts.DbSchema);

		builder.ConfigureByConvention();

		builder.Property(x => x.SystemStage)
			.HasConversion<int>()
			.IsRequired();
		builder.Property(x => x.StageKey)
			.HasMaxLength(AdmissionCrmConsts.MaxPlaybookCodeLength)
			.IsRequired();
		builder.Property(x => x.BaseSystemStage)
			.HasConversion<int>()
			.IsRequired();
		builder.Property(x => x.StageName)
			.HasMaxLength(AdmissionCrmConsts.MaxStageNameLength)
			.IsRequired();
		builder.Property(x => x.ColorHex)
			.HasMaxLength(AdmissionCrmConsts.MaxStageColorLength);
		builder.Property(x => x.OwnerResponsibility)
			.HasMaxLength(AdmissionCrmConsts.MaxStageOwnerResponsibilityLength);
		builder.Property(x => x.AllowedNextStagesJson)
			.HasMaxLength(AdmissionCrmConsts.MaxPipelineAllowedNextStagesJsonLength);

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.StageKey })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.SystemStage });
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.DisplayOrder });
	}
}
