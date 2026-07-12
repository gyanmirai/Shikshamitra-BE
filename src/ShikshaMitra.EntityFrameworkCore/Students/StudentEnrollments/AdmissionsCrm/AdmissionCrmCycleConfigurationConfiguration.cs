using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmCycleConfigurationConfiguration : IEntityTypeConfiguration<AdmissionCrmCycleConfiguration>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmCycleConfiguration> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmCycleConfigurations",
			ShikshaMitraConsts.DbSchema);

		builder.ConfigureByConvention();

		builder.Property(x => x.CycleName)
			.HasMaxLength(AdmissionCrmConsts.MaxCycleNameLength)
			.IsRequired();
		builder.Property(x => x.Notes)
			.HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.Property(x => x.MeritRankingMode)
			.HasConversion<int>()
			.IsRequired();
		builder.Property(x => x.ExamReadinessFields)
			.HasConversion<int>()
			.IsRequired();
		builder.Property(x => x.LeadScoringRulesJson)
			.HasMaxLength(AdmissionCrmConsts.MaxScoringRulesJsonLength)
			.IsRequired();
		builder.Property(x => x.LeadRoutingRulesJson)
			.HasMaxLength(AdmissionCrmConsts.MaxRoutingRulesJsonLength)
			.IsRequired();
		builder.Property(x => x.JourneyRulesJson)
			.HasMaxLength(AdmissionCrmConsts.MaxRoutingRulesJsonLength)
			.IsRequired();
		builder.Property(x => x.ApplicantChecklistJson)
			.HasMaxLength(AdmissionCrmConsts.MaxRoutingRulesJsonLength)
			.IsRequired();
		builder.Property(x => x.ChannelSettingsJson)
			.HasMaxLength(AdmissionCrmConsts.MaxRoutingRulesJsonLength)
			.IsRequired();
		builder.Property(x => x.SlaPolicyJson).HasColumnType("jsonb").IsRequired();
		builder.Property(x => x.DefaultExamPassingPercentage)
			.HasPrecision(5, 2);

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false");
	}
}
