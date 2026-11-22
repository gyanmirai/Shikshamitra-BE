using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Communication.Notifications;

internal static class NotificationMapping
{
	internal static void Configure<T>(EntityTypeBuilder<T> builder, string table) where T : class
	{
		// ABP's optional TenantId convention conflicts with composite tenant keys introduced by dependent mappings.
		EntityTypeBuilder audit = builder;
		audit.TryConfigureConcurrencyStamp();
		audit.TryConfigureExtraProperties();
		audit.TryConfigureObjectExtensions();
		audit.TryConfigureMayHaveCreator();
		audit.TryConfigureMustHaveCreator();
		audit.TryConfigureSoftDelete();
		audit.TryConfigureDeletionTime();
		audit.TryConfigureDeletionAudited();
		audit.TryConfigureCreationTime();
		audit.TryConfigureLastModificationTime();
		audit.TryConfigureModificationAudited();
		builder.ToTable(table);
		builder.Property<System.Guid?>("TenantId").IsRequired();
		builder.HasAlternateKey("TenantId", "Id");
		builder.Property<string>("ConcurrencyStamp").IsConcurrencyToken().HasMaxLength(40);
	}
}
