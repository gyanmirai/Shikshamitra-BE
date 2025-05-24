using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students.Dashboard;

public class DashboardLayoutConfiguration : IEntityTypeConfiguration<DashboardLayout>
{
	public void Configure(EntityTypeBuilder<DashboardLayout> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "DashboardLayouts", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.UserId).IsRequired();
		builder.Property(x => x.Layouts).IsRequired();
		builder.Property(x => x.VisibleWidgets).IsRequired();
	}
}
