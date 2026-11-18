using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationTemplateHeader_Tests(NotificationPostgresFixture fixture) : IClassFixture<NotificationPostgresFixture>
{
	[Fact]
	public void Current_revision_reference_preserves_legacy_null_tenants_and_matches_its_own_header()
	{
		ShikshaMitraEfCoreEntityExtensionMappings.Configure();
		using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
			.UseNpgsql("Host=127.0.0.1;Database=notification_tests_model;Username=notification_tests").Options);
		var header = db.Model.FindEntityType(typeof(CommunicationTemplate))!;
		header.FindProperty(nameof(CommunicationTemplate.TenantId))!.IsNullable.ShouldBeTrue();
		header.FindProperty(nameof(CommunicationTemplate.ConcurrencyStamp))!.IsConcurrencyToken.ShouldBeTrue();
		header.GetForeignKeys().ShouldContain(fk => fk.PrincipalEntityType.ClrType == typeof(CommunicationTemplateRevision) &&
			fk.Properties.Select(x => x.Name).SequenceEqual(new[] { "TenantId", "Id", "CurrentNotificationRevisionId" }) &&
			fk.PrincipalKey.Properties.Select(x => x.Name).SequenceEqual(new[] { "TenantId", "TemplateId", "Id" }));
	}

	[NotificationPostgresFact]
	public async Task Header_cannot_point_to_another_tenant_or_another_template_revision()
	{
		var tenant = Guid.NewGuid();
		await using var db = fixture.CreateContext();
		var owner = Header(tenant); var other = Header(tenant); var foreign = Header(Guid.NewGuid());
		db.AddRange(owner, other, foreign); await db.SaveChangesAsync();
		var sameTenantWrongHeader = Revision(other); var wrongTenant = Revision(foreign);
		db.AddRange(sameTenantWrongHeader, wrongTenant); await db.SaveChangesAsync();
		owner.SetNotificationRevision(wrongTenant.Id);
		await ExpectAsync(() => db.SaveChangesAsync(), PostgresErrorCodes.ForeignKeyViolation);
		db.ChangeTracker.Clear();
		owner = await db.Set<CommunicationTemplate>().SingleAsync(x => x.Id == owner.Id);
		owner.SetNotificationRevision(sameTenantWrongHeader.Id);
		await ExpectAsync(() => db.SaveChangesAsync(), PostgresErrorCodes.ForeignKeyViolation);
	}

	[NotificationPostgresFact]
	public async Task A_current_revision_is_protected_until_its_header_reference_is_cleared()
	{
		await using var db = fixture.CreateContext();
		var header = Header(Guid.NewGuid()); db.Add(header); await db.SaveChangesAsync();
		var revision = Revision(header); db.Add(revision); await db.SaveChangesAsync();
		header.SetNotificationRevision(revision.Id); await db.SaveChangesAsync();
		db.ChangeTracker.Clear();
		var deletion = await db.Set<CommunicationTemplateRevision>().SingleAsync(x => x.Id == revision.Id);
		db.Remove(deletion);
		await ExpectAsync(() => db.SaveChangesAsync(), PostgresErrorCodes.RestrictViolation);
		db.ChangeTracker.Clear();
		var savedHeader = await db.Set<CommunicationTemplate>().SingleAsync(x => x.Id == header.Id);
		deletion = await db.Set<CommunicationTemplateRevision>().SingleAsync(x => x.Id == revision.Id);
		savedHeader.SetNotificationRevision(null); await db.SaveChangesAsync();
		db.Remove(deletion); await db.SaveChangesAsync();
		savedHeader.Id.ShouldBe(header.Id); savedHeader.TenantId.ShouldBe(header.TenantId);
	}

	[NotificationPostgresFact]
	public async Task All_three_creation_saves_roll_back_together()
	{
		var header = Header(Guid.NewGuid());
		await using (var db = fixture.CreateContext())
		{
			await using var transaction = await db.Database.BeginTransactionAsync();
			db.Add(header); await db.SaveChangesAsync();
			var revision = Revision(header); db.Add(revision); await db.SaveChangesAsync();
			header.SetNotificationRevision(revision.Id); await db.SaveChangesAsync();
			await transaction.RollbackAsync();
		}
		await using var verify = fixture.CreateContext();
		(await verify.Set<CommunicationTemplate>().AnyAsync(x => x.Id == header.Id)).ShouldBeFalse();
		(await verify.Set<CommunicationTemplateRevision>().AnyAsync(x => x.TemplateId == header.Id)).ShouldBeFalse();
	}

	[NotificationPostgresFact]
	public async Task A_legacy_host_header_cannot_bypass_reference_ownership_with_a_null_tenant()
	{
		await using var db = fixture.CreateContext();
		var host = Header(null); var school = Header(Guid.NewGuid());
		db.AddRange(host, school); await db.SaveChangesAsync();
		var revision = Revision(school); db.Add(revision); await db.SaveChangesAsync();
		var error = await Should.ThrowAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
			$"UPDATE \"CommunicationTemplates\" SET \"CurrentNotificationRevisionId\" = {revision.Id} WHERE \"Id\" = {host.Id}"));
		error.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
	}

	private static async Task ExpectAsync(Func<Task> action, string sqlState)
	{
		var exception = await Record.ExceptionAsync(action);
		exception.ShouldNotBeNull();
		Assert.True(exception is DbUpdateException, exception!.ToString());
		exception!.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(sqlState);
	}
	private static CommunicationTemplate Header(Guid? tenant) => new(Guid.NewGuid(), tenant) { TemplateName = "Template header test" };
	private static CommunicationTemplateRevision Revision(CommunicationTemplate header) => new(Guid.NewGuid(), header.TenantId!.Value,
		header.Id, 1, "Academic.Homework.Published", "Guardian", CommunicationChannelType.Email, "en", "Homework", "{{source_link}}", 1);
}
