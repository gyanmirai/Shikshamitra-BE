using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmEventConfiguration : IEntityTypeConfiguration<AdmissionCrmEvent>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmEvent> builder)
	{
		builder.ToTable("AppAdmissionCrmEvents");
		builder.ConfigureByConvention();
		builder.Property(x => x.Title).IsRequired().HasMaxLength(AdmissionCrmConsts.MaxDescriptionLength);
		builder.Property(x => x.Location).IsRequired().HasMaxLength(AdmissionCrmConsts.MaxDescriptionLength);
		builder.Property(x => x.Instructions).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.Property(x => x.CancellationReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.Property(x => x.EventType).HasConversion<int>().IsRequired();
		builder.Property(x => x.Status).HasConversion<int>().IsRequired();
		builder.Ignore(x => x.ReservedCount);
		builder.HasMany(x => x.Registrations)
			.WithOne()
			.HasForeignKey(x => x.EventId)
			.OnDelete(DeleteBehavior.Cascade);
		builder.Navigation(x => x.Registrations)
			.HasField("_registrations")
			.UsePropertyAccessMode(PropertyAccessMode.Field);
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.StartsAt });
		builder.HasIndex(x => new { x.TenantId, x.Status, x.StartsAt });
	}
}

public class AdmissionCrmEventRegistrationConfiguration :
	IEntityTypeConfiguration<AdmissionCrmEventRegistration>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmEventRegistration> builder)
	{
		builder.ToTable("AppAdmissionCrmEventRegistrations");
		builder.ConfigureByConvention();
		builder.Property(x => x.Status).HasConversion<int>().IsRequired();
		builder.Property(x => x.Notes).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.Property(x => x.CancellationReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.HasIndex(x => new { x.TenantId, x.EventId, x.Status });
		builder.HasIndex(x => new { x.TenantId, x.LeadId, x.Status });
	}
}
