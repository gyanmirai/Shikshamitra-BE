using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students;

public class StudentMedicalRecordConfiguration : IEntityTypeConfiguration<StudentMedicalRecord>
{
	public void Configure(EntityTypeBuilder<StudentMedicalRecord> builder)
	{
		builder.ToTable("StudentMedicalRecords");

		builder.ConfigureByConvention();

		// Configure scalar properties
		builder.Property(x => x.WeightKg).HasPrecision(5, 2);
		builder.Property(x => x.HeightCm).HasPrecision(5, 2);
		builder.Property(x => x.BodyMassIndex).HasPrecision(5, 2);
		builder.Property(x => x.Hemoglobin).HasPrecision(4, 2);
		builder.Property(x => x.Allergies).HasMaxLength(500);
		builder.Property(x => x.ChildImmunization).HasMaxLength(500);
		builder.Property(x => x.ImmunizationRemarks).HasMaxLength(500);
		builder.Property(x => x.BloodGroup).HasMaxLength(10);
		builder.Property(x => x.ChronicConditions).HasMaxLength(1000);
		builder.Property(x => x.CurrentMedications).HasMaxLength(500);
	}
}
