using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Curriculum.Settings;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
	public void Configure(EntityTypeBuilder<Subject> builder)
	{
		builder.ToTable(ShikshaMitraConsts.AcademicDbTablePrefix + "Subjects", ShikshaMitraConsts.DbSchema);
		// Apply default configurations based on ABP conventions.
		builder.ConfigureByConvention();
		builder.Property(x => x.DisplayName).HasMaxLength(SubjectConsts.MaxDisplayNameLength);
		builder.Property(x => x.Category).HasMaxLength(SubjectConsts.MaxCategoryLength);
		builder.Property(x => x.Icon).HasMaxLength(SubjectConsts.MaxIconLength);
		builder.Property(x => x.Description).HasMaxLength(SubjectConsts.MaxDescriptionLength);
		builder.Property(x => x.Status).HasConversion<int>().HasDefaultValue(SubjectMasterStatus.Active);
	}
}

public class SubjectAuditLogConfiguration : IEntityTypeConfiguration<SubjectAuditLog>
{
	public void Configure(EntityTypeBuilder<SubjectAuditLog> builder)
	{
		builder.ToTable(ShikshaMitraConsts.AcademicDbTablePrefix + "SubjectAuditLogs", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		
		builder.Property(x => x.Action).IsRequired().HasMaxLength(50);
		builder.Property(x => x.Module).IsRequired().HasMaxLength(100);
		builder.Property(x => x.SubModule).HasMaxLength(100);
		builder.Property(x => x.EntityType).HasMaxLength(100);
		builder.Property(x => x.EntityName).HasMaxLength(256);
		builder.Property(x => x.Reason).HasMaxLength(500);
		
		builder.Property(x => x.ActorNameSnapshot).HasMaxLength(256);
		builder.Property(x => x.ActorRoleSnapshot).HasMaxLength(100);
		builder.Property(x => x.ActorEmailSnapshot).HasMaxLength(256);
		builder.Property(x => x.Source).HasMaxLength(50);
		builder.Property(x => x.IpAddress).HasMaxLength(64);
		builder.Property(x => x.Device).HasMaxLength(64);
		builder.Property(x => x.Browser).HasMaxLength(64);
		builder.Property(x => x.OperatingSystem).HasMaxLength(64);
		builder.Property(x => x.RequestPath).HasMaxLength(512);
		builder.Property(x => x.HttpMethod).HasMaxLength(16);
		builder.Property(x => x.CorrelationId).HasMaxLength(64);
		
		builder.HasIndex(x => x.SubjectId);
		builder.HasIndex(x => x.ChangedAt);
	}
}
