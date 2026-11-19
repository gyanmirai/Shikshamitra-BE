using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSubstitute;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionInquiryFamilyConfirmationPostgres_Tests
{
	[AssessmentRosterPostgresFact]
	public async Task Pending_family_confirmation_fence_refreshes_waiting_applicant_and_rejects_conflicting_modified_identity()
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ASSESSMENT_ROSTER_TEST_POSTGRES"));
		connection.Database += "_handoff";
		if (connection.Host is not ("localhost" or "127.0.0.1" or "::1") || connection.Database == null ||
			!Regex.IsMatch(connection.Database, "^admissions_assessment_tests_[a-z0-9_]{1,32}$"))
			throw new InvalidOperationException("Only a previously absent disposable loopback test database is allowed.");
		await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connection.ConnectionString) { Database = "postgres", Pooling = false }.ConnectionString);
		await admin.OpenAsync();
		await using (var exists = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname=@name", admin))
		{
			exists.Parameters.AddWithValue("name", connection.Database);
			Convert.ToInt64(await exists.ExecuteScalarAsync()).ShouldBe(0, "Existing databases must never be reused or removed.");
		}
		await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{connection.Database}\"", admin)) await create.ExecuteNonQueryAsync();
		try
		{
			var model = new ModelBuilder();
			Map<AdmissionInquiry>(model, "AppAdmissionInquiries", ["Id", "TenantId", "ApplicantId", "FamilyId", "IsDeleted", "ConcurrencyStamp", "ExtraProperties"]);
			Map<AdmissionApplicant>(model, "AppAdmissionApplicants", ["Id", "TenantId", "FamilyId", "ExistingStudentId", "Status", "IsDeleted", "ConcurrencyStamp", "ExtraProperties", "FirstName", "ReservedStudentId"]);
			var options = new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(connection.ConnectionString).UseModel(model.FinalizeModel()).Options;
			await using var winner = new ShikshaMitraDbContext(options);
			await using var waiter = new ShikshaMitraDbContext(options);
			await winner.Database.EnsureCreatedAsync();
			var tenant = Guid.NewGuid(); var inquiryId = Guid.NewGuid(); var applicantId = Guid.NewGuid(); var unrelatedId = Guid.NewGuid();
			var originalFamily = Guid.NewGuid(); var confirmedFamily = Guid.NewGuid(); var sourceId = Guid.NewGuid();
			const string emptyProperties = "{}";
			foreach (var id in new[] { applicantId, unrelatedId })
				await winner.Database.ExecuteSqlInterpolatedAsync($"""
					INSERT INTO "AppAdmissionApplicants" ("Id", "TenantId", "FamilyId", "ExistingStudentId", "Status", "IsDeleted", "ConcurrencyStamp", "ExtraProperties", "FirstName", "ReservedStudentId")
					VALUES ({id}, {tenant}, {originalFamily}, NULL, 0, false, 'initial', CAST({emptyProperties} AS jsonb), 'Original child', {Guid.NewGuid()})
					""");
			await winner.Database.ExecuteSqlInterpolatedAsync($"""
				INSERT INTO "AppAdmissionInquiries" ("Id", "TenantId", "ApplicantId", "FamilyId", "IsDeleted", "ConcurrencyStamp", "ExtraProperties")
				VALUES ({inquiryId}, {tenant}, {applicantId}, {originalFamily}, false, 'initial', CAST({emptyProperties} AS jsonb))
				""");
			var stale = await waiter.Set<AdmissionApplicant>().SingleAsync(item => item.Id == applicantId);
			var unrelated = await waiter.Set<AdmissionApplicant>().SingleAsync(item => item.Id == unrelatedId);
			unrelated.UpdateProfile("Unsaved unrelated child", null, null, null, null);
			var currentTenant = Substitute.For<ICurrentTenant>(); currentTenant.Id.Returns(tenant);
			var provider = Substitute.For<IDbContextProvider<ShikshaMitraDbContext>>(); provider.GetDbContextAsync().Returns(waiter);
			var repository = new FixtureInquiryRepository(provider, currentTenant, waiter, tenant);
			await Should.ThrowAsync<InvalidOperationException>(() => repository.FindForApplicationStartAsync(inquiryId));
			await using (var winningTransaction = await winner.Database.BeginTransactionAsync())
			await using (var waitingTransaction = await waiter.Database.BeginTransactionAsync())
			{
				await HoldInquiryAsync(winner, inquiryId, tenant);
				var waiting = repository.FindForApplicationStartAsync(inquiryId);
				(await Task.WhenAny(waiting, Task.Delay(150))).ShouldNotBe(waiting);
				await CommitFamilyAsync(winner, inquiryId, applicantId, confirmedFamily, sourceId);
				await winningTransaction.CommitAsync();
				var refreshed = (await waiting.WaitAsync(TimeSpan.FromSeconds(5))).ShouldNotBeNull();
				refreshed.FamilyId.ShouldBe(confirmedFamily);
				stale.FamilyId.ShouldBe(confirmedFamily);
				stale.ExtraProperties["SiblingParentConfirmation"].ShouldNotBeNull().ToString().ShouldNotBeNull().ShouldContain(sourceId.ToString());
				unrelated.FirstName.ShouldBe("Unsaved unrelated child");
				waiter.Entry(unrelated).State.ShouldBe(EntityState.Modified);
				await waitingTransaction.CommitAsync();
			}
			stale.UpdateProfile("Unsaved target child", null, null, null, null);
			var nextFamily = Guid.NewGuid();
			await using (var winningTransaction = await winner.Database.BeginTransactionAsync())
			await using (var waitingTransaction = await waiter.Database.BeginTransactionAsync())
			{
				await HoldInquiryAsync(winner, inquiryId, tenant);
				var waiting = repository.FindForApplicationStartAsync(inquiryId);
				(await Task.WhenAny(waiting, Task.Delay(150))).ShouldNotBe(waiting);
				await CommitFamilyAsync(winner, inquiryId, applicantId, nextFamily, Guid.NewGuid());
				await winningTransaction.CommitAsync();
				(await Should.ThrowAsync<BusinessException>(() => waiting)).Code.ShouldBe(AdmissionInquiryManagementErrorCodes.ConcurrencyConflict);
				stale.FirstName.ShouldBe("Unsaved target child");
				stale.FamilyId.ShouldBe(confirmedFamily);
				waiter.Entry(stale).State.ShouldBe(EntityState.Modified);
				await waitingTransaction.RollbackAsync();
			}
			await waiter.Entry(stale).ReloadAsync();
			stale.UpdateProfile("Safe pending edit", null, null, null, null);
			await using (var transaction = await waiter.Database.BeginTransactionAsync())
			{
				(await repository.FindForApplicationStartAsync(inquiryId)).ShouldNotBeNull();
				stale.FirstName.ShouldBe("Safe pending edit"); stale.FamilyId.ShouldBe(nextFamily);
				waiter.Entry(stale).State.ShouldBe(EntityState.Modified);
				await transaction.RollbackAsync();
			}
			await VerifyMutualAndCentralSourceFencesAsync(options, tenant);
		}
		finally
		{
			NpgsqlConnection.ClearAllPools();
			await using var drop = new NpgsqlCommand($"DROP DATABASE \"{connection.Database}\" WITH (FORCE)", admin);
			await drop.ExecuteNonQueryAsync();
		}
	}

	private static async Task VerifyMutualAndCentralSourceFencesAsync(DbContextOptions<ShikshaMitraDbContext> options, Guid tenantId)
	{
		await using var first = new ShikshaMitraDbContext(options);
		await using var second = new ShikshaMitraDbContext(options);
		var tenant = Substitute.For<ICurrentTenant>(); tenant.Id.Returns(tenantId);
		static EfCoreAdmissionApplicantRepository Locks(ShikshaMitraDbContext db, ICurrentTenant current)
		{
			var provider = Substitute.For<IDbContextProvider<ShikshaMitraDbContext>>(); provider.GetDbContextAsync().Returns(db);
			return new(provider, current);
		}
		async Task<(Guid Applicant, Guid Inquiry)> Seed(Guid family)
		{
			var applicant = Guid.NewGuid(); var inquiry = Guid.NewGuid(); const string empty = "{}";
			await first.Database.ExecuteSqlInterpolatedAsync($"""
				INSERT INTO "AppAdmissionApplicants" ("Id", "TenantId", "FamilyId", "ExistingStudentId", "Status", "IsDeleted", "ConcurrencyStamp", "ExtraProperties", "FirstName", "ReservedStudentId")
				VALUES ({applicant}, {tenantId}, {family}, NULL, 0, false, 'initial', CAST({empty} AS jsonb), 'Pending child', {Guid.NewGuid()})
				""");
			await first.Database.ExecuteSqlInterpolatedAsync($"""
				INSERT INTO "AppAdmissionInquiries" ("Id", "TenantId", "ApplicantId", "FamilyId", "IsDeleted", "ConcurrencyStamp", "ExtraProperties")
				VALUES ({inquiry}, {tenantId}, {applicant}, {family}, false, 'initial', CAST({empty} AS jsonb))
				""");
			return (applicant, inquiry);
		}
		var familyA = Guid.NewGuid(); var familyB = Guid.NewGuid();
		var a = await Seed(familyA); var b = await Seed(familyB);
		var secondA = await second.Set<AdmissionApplicant>().SingleAsync(item => item.Id == a.Applicant);
		var secondB = await second.Set<AdmissionApplicant>().SingleAsync(item => item.Id == b.Applicant);
		var firstLocks = Locks(first, tenant); var secondLocks = Locks(second, tenant);
		await using (var winner = await first.Database.BeginTransactionAsync())
		await using (var loser = await second.Database.BeginTransactionAsync())
		{
			await firstLocks.LockForSiblingParentSelectionAsync([a.Applicant, b.Applicant]);
			var waiting = secondLocks.LockForSiblingParentSelectionAsync([b.Applicant, a.Applicant]);
			(await Task.WhenAny(waiting, Task.Delay(150))).ShouldNotBe(waiting);
			await HoldInquiryAsync(first, b.Inquiry, tenantId);
			await CommitFamilyAsync(first, b.Inquiry, b.Applicant, familyA, a.Applicant);
			await winner.CommitAsync();
			await waiting.WaitAsync(TimeSpan.FromSeconds(5));
			secondA.FamilyId.ShouldBe(familyA);
			secondB.FamilyId.ShouldBe(familyA);
			secondB.FamilyId.ShouldNotBe(familyB, "The opposite A->old-family-B proof must fail current source membership after the sorted pair fence.");
			await loser.RollbackAsync();
		}
		var oldSourceFamily = Guid.NewGuid(); var newSourceFamily = Guid.NewGuid();
		var source = await Seed(oldSourceFamily); var destination = await Seed(newSourceFamily);
		await second.Set<AdmissionApplicant>().SingleAsync(item => item.Id == source.Applicant);
		var applicants = Substitute.For<IRepository<AdmissionApplicant, Guid>>();
		applicants.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call =>
			second.Set<AdmissionApplicant>().FirstOrDefaultAsync(item => item.Id == call.Arg<Guid>()));
		var matcher = new AdmissionDirectFamilyMatchService(null!, null!, applicants, secondLocks, null!, null!, null!, null!);
		using var services = new ServiceCollection().AddSingleton(tenant).BuildServiceProvider();
		matcher.LazyServiceProvider = new AbpLazyServiceProvider(services);
		await using (var winner = await first.Database.BeginTransactionAsync())
		await using (var waitingCreate = await second.Database.BeginTransactionAsync())
		{
			await firstLocks.LockForSiblingParentSelectionAsync([source.Applicant, destination.Applicant]);
			var waiting = matcher.ResolveParentAsync(oldSourceFamily, source.Applicant, "father", "9876543210", "parent@example.com", AdmissionErrorCodes.InquiryFamilyMismatch);
			(await Task.WhenAny(waiting, Task.Delay(150))).ShouldNotBe(waiting);
			await HoldInquiryAsync(first, source.Inquiry, tenantId);
			await CommitFamilyAsync(first, source.Inquiry, source.Applicant, newSourceFamily, destination.Applicant);
			await winner.CommitAsync();
			(await Should.ThrowAsync<BusinessException>(() => waiting)).Code.ShouldBe(AdmissionErrorCodes.InquiryFamilyMismatch);
			await waitingCreate.RollbackAsync();
		}
	}

	private static async Task<int> HoldInquiryAsync(ShikshaMitraDbContext db, Guid inquiryId, Guid tenant)
	{
		await db.Database.ExecuteSqlInterpolatedAsync($"""
			SELECT 1 FROM "AppAdmissionApplicants" AS applicant JOIN "AppAdmissionInquiries" AS inquiry ON inquiry."ApplicantId" = applicant."Id"
			WHERE inquiry."Id" = {inquiryId} AND inquiry."TenantId" = {tenant} FOR UPDATE OF applicant
			""");
		return await db.Database.ExecuteSqlInterpolatedAsync($"""SELECT 1 FROM "AppAdmissionInquiries" WHERE "Id" = {inquiryId} AND "TenantId" = {tenant} FOR UPDATE""");
	}
	private static async Task CommitFamilyAsync(ShikshaMitraDbContext db, Guid inquiryId, Guid applicantId, Guid family, Guid source)
	{
		var proof = JsonSerializer.Serialize(new AdmissionSiblingParentConfirmation(family, source, "father", "parent@example.com", Guid.NewGuid(), DateTime.UtcNow, "9876543210", "parent@example.com"));
		var properties = JsonSerializer.Serialize(new Dictionary<string, string> { ["SiblingParentConfirmation"] = proof, ["ParentAccountRelationship"] = "father", ["ParentAccountFlowVersion"] = "1" });
		await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "AppAdmissionApplicants" SET "FamilyId" = {family}, "ExtraProperties" = CAST({properties} AS jsonb) WHERE "Id" = {applicantId}""");
		await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "AppAdmissionInquiries" SET "FamilyId" = {family}, "ConcurrencyStamp" = {Guid.NewGuid().ToString("N")} WHERE "Id" = {inquiryId}""");
	}
	private static void Map<T>(ModelBuilder builder, string table, string[] columns) where T : class
	{
		var entity = builder.Entity<T>(); entity.ToTable(table); entity.HasKey("Id");
		foreach (var property in typeof(T).GetProperties())
		{
			if (columns.Contains(property.Name)) entity.Property(property.PropertyType, property.Name);
			else entity.Ignore(property.Name);
		}
		entity.Property<ExtraPropertyDictionary>("ExtraProperties").HasConversion(value => Serialize(value), value => Deserialize(value))
			.HasColumnType("jsonb").Metadata.SetValueComparer(new ValueComparer<ExtraPropertyDictionary>(
				(left, right) => Serialize(left!) == Serialize(right!), value => Serialize(value).GetHashCode(), value => Deserialize(Serialize(value))));
	}
	private static string Serialize(ExtraPropertyDictionary properties) => JsonSerializer.Serialize(properties);
	private static ExtraPropertyDictionary Deserialize(string json) => JsonSerializer.Deserialize<ExtraPropertyDictionary>(json)!;
	[DisableConventionalRegistration]
	private sealed class FixtureInquiryRepository(IDbContextProvider<ShikshaMitraDbContext> provider, ICurrentTenant tenant,
		ShikshaMitraDbContext db, Guid tenantId) : EfCoreAdmissionInquiryRepository(provider, tenant)
	{
		public override Task<AdmissionInquiry?> FindAsync(Guid id, bool includeDetails = true, CancellationToken cancellationToken = default) =>
			db.Set<AdmissionInquiry>().FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
	}
}
