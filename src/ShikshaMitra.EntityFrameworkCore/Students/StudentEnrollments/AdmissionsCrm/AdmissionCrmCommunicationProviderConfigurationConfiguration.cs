using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmCommunicationProviderConfigurationConfiguration :
	IEntityTypeConfiguration<AdmissionCrmCommunicationProviderConfiguration>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmCommunicationProviderConfiguration> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmCommunicationProviderConfigurations",
			ShikshaMitraConsts.DbSchema);

		builder.ConfigureByConvention();

		builder.Property(x => x.Channel)
			.HasConversion<int>()
			.IsRequired();
		builder.Property(x => x.Status)
			.HasMaxLength(120)
			.IsRequired();
		builder.Property(x => x.ProviderName)
			.HasMaxLength(AdmissionCrmConsts.MaxNameLength);
		builder.Property(x => x.SenderId)
			.HasMaxLength(AdmissionCrmConsts.MaxNameLength);
		builder.Property(x => x.SettingsJson)
			.HasMaxLength(AdmissionCrmConsts.MaxRoutingRulesJsonLength);
		builder.Property(x => x.WebhookUrl)
			.HasMaxLength(AdmissionCrmConsts.MaxUrlLength);

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Channel })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Status });
	}
}
