using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Academic.HomeworkAssignment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Examination;

public class GradingScaleConfiguration : IEntityTypeConfiguration<GradingScale>
{
	public void Configure(EntityTypeBuilder<GradingScale> builder)
	{
		// Table configuration
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "GradingScales",
			ShikshaMitraConsts.DbSchema
		);

		// ABP conventions (CreationTime, CreatorId, etc.)
		builder.ConfigureByConvention();

		builder.Property(x => x.Description).HasMaxLength(512);
		builder.Property(x => x.Status).HasDefaultValue(SettingStatus.Active);

		// Configure GradeRanges relationship
		builder.HasMany(gs => gs.GradeRanges)
			.WithOne()
			.HasForeignKey(gr => gr.GradingScaleId)
			.IsRequired()
			.OnDelete(DeleteBehavior.Cascade);
	}
}
