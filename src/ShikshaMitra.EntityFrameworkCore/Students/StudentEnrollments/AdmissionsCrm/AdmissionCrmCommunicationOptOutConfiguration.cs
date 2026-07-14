using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmCommunicationOptOutConfiguration : IEntityTypeConfiguration<AdmissionCrmCommunicationOptOut>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmCommunicationOptOut> builder)
	{
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmCommunicationOptOuts",
			ShikshaMitraConsts.DbSchema);

		builder.ConfigureByConvention();

		builder.Property(x => x.Channel)
			.HasConversion<int>()
			.IsRequired();
		builder.Property(x => x.ContactValue)
			.HasMaxLength(AdmissionCrmConsts.MaxEmailLength)
			.IsRequired();
		builder.Property(x => x.NormalizedContactValue)
			.HasMaxLength(AdmissionCrmConsts.MaxEmailLength)
			.IsRequired();
		builder.Property(x => x.Reason)
			.HasMaxLength(AdmissionCrmConsts.MaxNotesLength)
			.IsRequired();
		builder.Property(x => x.OptInReason)
			.HasMaxLength(AdmissionCrmConsts.MaxNotesLength);

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Channel, x.NormalizedContactValue, x.IsActive });
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.LeadId });
	}
}
