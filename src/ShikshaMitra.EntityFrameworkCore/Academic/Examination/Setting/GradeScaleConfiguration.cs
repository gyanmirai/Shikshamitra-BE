using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Examination.Setting;
public class GradeScaleConfiguration : IEntityTypeConfiguration<GradeScale>
{
	public void Configure(EntityTypeBuilder<GradeScale> builder)
	{
		// Table Name
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "GradeScales",
			ShikshaMitraConsts.DbSchema
		);

		// Primary Key
		builder.HasKey(x => x.Id);

		// Properties
		builder.Property(x => x.Title)
			   .IsRequired()
			   .HasMaxLength(128);

		builder.Property(x => x.TotalMarks)
			   .IsRequired();

		builder.Property(x => x.EducationMediumId)
			   .IsRequired();

		// Relationships
		builder.HasOne(x => x.EducationMedium)
			   .WithMany()
			   .HasForeignKey(x => x.EducationMediumId)
			   .OnDelete(DeleteBehavior.Restrict);

		builder.HasMany(x => x.GradeRanges)
			   .WithOne(x => x.GradeScale)
			   .HasForeignKey(x => x.GradeScaleId)
			   .OnDelete(DeleteBehavior.Cascade);

		// Indexes (optional, but improves performance)
		builder.HasIndex(x => new { x.Title, x.EducationMediumId })
			   .IsUnique();
	}
}
