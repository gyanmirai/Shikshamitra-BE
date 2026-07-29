using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmAdmissionYearSetupConfiguration :
	IEntityTypeConfiguration<AdmissionCrmAdmissionYearSetup>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmAdmissionYearSetup> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmAdmissionYearSetups",
			ShikshaMitraConsts.DbSchema);

		builder.ConfigureByConvention();
		builder.Property(x => x.Status).HasConversion<int>().IsRequired();
		builder.Property(x => x.PlacementTiming).HasConversion<int>().IsRequired();
		builder.Property(x => x.InquiryOpenDate).HasColumnType("date").IsRequired();
		builder.Property(x => x.InquiryCloseDate).HasColumnType("date").IsRequired();
		builder.Property(x => x.RegistrationCloseDate).HasColumnType("date").IsRequired();
		builder.Property(x => x.ActivationTargetDate).HasColumnType("date").IsRequired();
		builder.Property(x => x.TargetStandardIdsJson).HasColumnType("jsonb").IsRequired();
		builder.Property(x => x.RouteTypesJson).HasColumnType("jsonb").IsRequired();
		builder.Property(x => x.StepOwnersJson).HasColumnType("jsonb").IsRequired();
		builder.Property(x => x.PublishedSnapshotJson).HasColumnType("jsonb");
		builder.Property(x => x.CloseReason)
			.HasMaxLength(AdmissionCrmConsts.MaxNotesLength);

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Version })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Status })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false AND \"Status\" IN (0, 1)");
	}
}
