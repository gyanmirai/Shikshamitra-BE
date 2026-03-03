using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Setting.GeneralSetting
{
	public class LibraryTimingConfiguration : IEntityTypeConfiguration<LibraryTiming>
	{
		public void Configure(EntityTypeBuilder<LibraryTiming> builder)
		{
			builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "LibraryTimings", ShikshaMitraConsts.DbSchema);
			builder.ConfigureByConvention();

			builder.Property(x => x.Day)
				.IsRequired()
				.HasMaxLength(20);

			builder.Property(x => x.StartTime)
				.HasMaxLength(20);

			builder.Property(x => x.EndTime)
				.HasMaxLength(20);

			builder.Property(x => x.Status)
				.IsRequired();

			builder.Property(x => x.AcademicYearId);
		}
	}
}
