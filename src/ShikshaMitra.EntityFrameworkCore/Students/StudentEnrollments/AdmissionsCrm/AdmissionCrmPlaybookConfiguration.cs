using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPlaybookConfiguration : IEntityTypeConfiguration<AdmissionCrmPlaybook>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmPlaybook> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmPlaybooks",
			ShikshaMitraConsts.DbSchema);

		builder.ConfigureByConvention();
		builder.Property(x => x.Name)
			.HasMaxLength(AdmissionCrmConsts.MaxTemplateNameLength)
			.IsRequired();
		builder.Property(x => x.Status)
			.HasConversion<int>()
			.IsRequired();
		builder.Property(x => x.RegistrationPath)
			.HasConversion<int>()
			.IsRequired();
		builder.Property(x => x.ApplicabilityJson)
			.HasMaxLength(AdmissionCrmConsts.MaxPlaybookJsonLength)
			.IsRequired();
		builder.Property(x => x.StagesJson)
			.HasMaxLength(AdmissionCrmConsts.MaxPlaybookJsonLength)
			.IsRequired();
		builder.Property(x => x.RoundsJson)
			.HasMaxLength(AdmissionCrmConsts.MaxPlaybookJsonLength)
			.IsRequired();
		builder.Property(x => x.SeatPlansJson)
			.HasMaxLength(AdmissionCrmConsts.MaxPlaybookJsonLength)
			.IsRequired();
		builder.Property(x => x.ExamDefinitionsJson)
			.HasMaxLength(AdmissionCrmConsts.MaxPlaybookJsonLength)
			.IsRequired();
		builder.Property(x => x.SlotTemplatesJson)
			.HasMaxLength(AdmissionCrmConsts.MaxPlaybookJsonLength)
			.IsRequired();
		builder.Property(x => x.AssignmentRulesJson)
			.HasMaxLength(AdmissionCrmConsts.MaxPlaybookJsonLength)
			.IsRequired();
		builder.Property(x => x.Notes)
			.HasMaxLength(AdmissionCrmConsts.MaxNotesLength);

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Status });
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Name, x.Version })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false");
	}
}
