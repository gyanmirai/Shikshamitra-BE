using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.Attendance;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.Attendance;

public class AttendanceDayConfiguration : IEntityTypeConfiguration<AttendanceDay>
{
	public void Configure(EntityTypeBuilder<AttendanceDay> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AttendanceDays", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.HasKey(ad => ad.Id);

		// Properties
		builder.Property(ad => ad.AcademicYearId).IsRequired();
		builder.Property(ad => ad.StandardId).IsRequired();
		builder.Property(ad => ad.DivisionId).IsRequired();
		builder.Property(ad => ad.Date).IsRequired().HasColumnType("date");
		builder.Property(ad => ad.IsLocked).IsRequired().HasDefaultValue(false);
		builder.Property(ad => ad.MarkedByUserId).IsRequired();

		// Indexes
		builder.HasIndex(ad => new { ad.AcademicYearId, ad.StandardId, ad.DivisionId, ad.Date });

		// Unique constraint: One attendance day per division per date
		builder.HasIndex(ad => new { ad.AcademicYearId, ad.StandardId, ad.DivisionId, ad.Date })
			.IsUnique()
			.HasDatabaseName("UX_AttendanceDay_Unique");

		// Navigation - AttendanceDay has many AttendanceEntries
		builder.HasMany(ad => ad.Entries)
			.WithOne(ae => ae.AttendanceDay)
			.HasForeignKey(ae => ae.AttendanceDayId)
			.OnDelete(DeleteBehavior.Cascade);
	}
}
