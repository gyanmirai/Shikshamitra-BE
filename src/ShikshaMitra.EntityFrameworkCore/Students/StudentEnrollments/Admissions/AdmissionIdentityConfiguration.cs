using Microsoft.EntityFrameworkCore; using Microsoft.EntityFrameworkCore.Metadata; using Microsoft.EntityFrameworkCore.Metadata.Builders; using ShikshaMitra.Students.StudentEnrollments.Admissions; using Volo.Abp.EntityFrameworkCore.Modeling;
namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;
public class AdmissionIdentityConfiguration : IEntityTypeConfiguration<AdmissionFamily>, IEntityTypeConfiguration<AdmissionGuardian>, IEntityTypeConfiguration<AdmissionApplicant>
{
	public void Configure(EntityTypeBuilder<AdmissionFamily> b) { b.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionFamilies", ShikshaMitraConsts.DbSchema); b.ConfigureByConvention(); b.ConfigureSoftDeleteDefault(); b.Property(x => x.FamilyNumber).IsRequired().HasMaxLength(64); b.HasIndex(x => new { x.TenantId, x.FamilyNumber }).IsUnique(); }
	void IEntityTypeConfiguration<AdmissionGuardian>.Configure(EntityTypeBuilder<AdmissionGuardian> b) { b.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionGuardians", ShikshaMitraConsts.DbSchema); b.ConfigureByConvention(); b.ConfigureSoftDeleteDefault(); b.Property(x => x.FirstName).IsRequired().HasMaxLength(128); b.Property(x => x.Mobile).IsRequired().HasMaxLength(32); b.Property(x => x.Email).HasMaxLength(256); b.HasIndex(x => new { x.TenantId, x.Mobile }); }
	void IEntityTypeConfiguration<AdmissionApplicant>.Configure(EntityTypeBuilder<AdmissionApplicant> b)
	{
		b.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionApplicants", ShikshaMitraConsts.DbSchema,
			table => table.HasCheckConstraint("CK_AdmissionApplicants_ReservedStudentId",
				"\"ReservedStudentId\" <> '00000000-0000-0000-0000-000000000000'"));
		b.ConfigureByConvention();
		b.ConfigureSoftDeleteDefault();
		b.Property(x => x.FirstName).IsRequired().HasMaxLength(128);
		b.Property(x => x.LastName).HasMaxLength(128);
		b.Property(x => x.AddressLine1).HasMaxLength(256);
		b.Property(x => x.AddressLine2).HasMaxLength(256);
		b.Property(x => x.City).HasMaxLength(128);
		b.Property(x => x.State).HasMaxLength(128);
		b.Property(x => x.Country).HasMaxLength(128);
		b.Property(x => x.PostalCode).HasMaxLength(32);
		b.Property(x => x.ReservedStudentId).IsRequired().ValueGeneratedNever()
			.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
		// Student primary keys are global; reservations stay unique across tenants and deleted applicants.
		b.HasIndex(x => x.ReservedStudentId).IsUnique()
			.HasDatabaseName("UX_AdmissionApplicants_ReservedStudentId");
		b.HasIndex(x => new { x.TenantId, x.FamilyId });
	}
}
