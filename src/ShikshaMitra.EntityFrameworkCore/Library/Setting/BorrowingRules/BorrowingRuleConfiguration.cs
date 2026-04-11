using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Setting.BorrowingRules;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Setting.BorrowingRules
{
	public class BorrowingRuleConfiguration : IEntityTypeConfiguration<BorrowingRule>
	{
		public void Configure(EntityTypeBuilder<BorrowingRule> builder)
		{
			builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "BorrowingRules", ShikshaMitraConsts.DbSchema);
			builder.ConfigureByConvention();

			builder.Property(x => x.AcademicYearId)
				.IsRequired();

			builder.Property(x => x.Role)
				.IsRequired();

			builder.Property(x => x.MaxBooksAtTime)
				.IsRequired();

			builder.Property(x => x.ReservationLimit)
				.IsRequired();

			builder.HasIndex(x => new { x.AcademicYearId, x.Role }).IsUnique();
		}
	}
}
