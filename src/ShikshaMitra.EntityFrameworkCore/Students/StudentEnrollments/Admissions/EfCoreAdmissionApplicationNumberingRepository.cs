using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class EfCoreAdmissionApplicationNumberingRepository(IDbContextProvider<ShikshaMitraDbContext> provider, ICurrentTenant tenant)
	: IAdmissionApplicationNumberingRepository, ITransientDependency
{
	private Guid StoredTenantId => tenant.Id ?? Guid.Empty;
	public async Task LockAsync()
	{
		var db = await provider.GetDbContextAsync();
		if (!db.Database.IsNpgsql()) return; // SQLite integration tests; production is PostgreSQL.
		if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Number allocation requires a transaction.");
		var key = BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes($"admission-numbering:{StoredTenantId}")), 0);
		await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})");
	}
	public async Task<AdmissionApplicationNumberingRule?> GetCurrentAsync() =>
		await (await provider.GetDbContextAsync()).Set<AdmissionApplicationNumberingRule>().AsNoTracking()
			.Where(x => x.StoredTenantId == StoredTenantId).OrderByDescending(x => x.Version).FirstOrDefaultAsync();
	public async Task<bool> HasIssuedAsync() => await (await provider.GetDbContextAsync()).Set<AdmissionApplicationNumberCounter>()
		.AnyAsync(x => x.StoredTenantId == StoredTenantId);
	public async Task InsertRuleAsync(AdmissionApplicationNumberingRule rule)
	{
		var db = await provider.GetDbContextAsync();
		await db.Set<AdmissionApplicationNumberingRule>().AddAsync(rule);
		await db.SaveChangesAsync();
	}
	public async Task<bool> NumberExistsAsync(string number) => await (await provider.GetDbContextAsync()).Set<AdmissionApplication>()
		.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenant.Id && x.ApplicationNumber == number);
	public async Task<long> TakeNextAsync(AdmissionApplicationNumberingRule rule, string scopeKey, string yearText)
	{
		var db = await provider.GetDbContextAsync();
		var next = await PeekNextAsync(rule, scopeKey, yearText);
		var counter = await db.Set<AdmissionApplicationNumberCounter>().SingleOrDefaultAsync(x => x.StoredTenantId == StoredTenantId && x.ScopeKey == scopeKey);
		if (counter == null)
		{
			counter = new(Guid.NewGuid(), tenant.Id, scopeKey, rule.StartingNumber);
			await db.Set<AdmissionApplicationNumberCounter>().AddAsync(counter);
		}
		counter.EnsureFloor(next, rule.Version, yearText);
		var value = counter.TakeNext();
		await db.SaveChangesAsync();
		return value;
	}
	// Both preview and allocation use this read-only calculation; preview never creates a counter.
	public async Task<long> PeekNextAsync(AdmissionApplicationNumberingRule rule, string scopeKey, string yearText)
	{
		var db = await provider.GetDbContextAsync();
		var counter = await db.Set<AdmissionApplicationNumberCounter>().AsNoTracking()
			.SingleOrDefaultAsync(x => x.StoredTenantId == StoredTenantId && x.ScopeKey == scopeKey);
		var next = Math.Max(rule.StartingNumber, counter?.NextValue ?? 1);
		// Once adopted, registrations only advance this counter; do not rescan history on every allocation.
		if (counter?.LastRuleVersion == rule.Version && counter.LastYearText == yearText) return next;
		var legacy = db.Set<AdmissionReferenceSequence>().AsNoTracking()
			.Where(x => x.StoredTenantId == StoredTenantId && x.Kind == AdmissionReferenceKind.Application);
		if (rule.ResetPolicy == AdmissionNumberResetPolicy.AcademicYear)
		{
			var yearId = Guid.ParseExact(scopeKey["academic:".Length..], "N");
			var numbers = await (from application in db.Set<AdmissionApplication>().IgnoreQueryFilters().AsNoTracking()
				join cycle in db.Set<AdmissionCycle>().IgnoreQueryFilters().AsNoTracking() on application.AdmissionCycleId equals cycle.Id
				where application.TenantId == tenant.Id && cycle.TenantId == tenant.Id && cycle.AcademicYearId == yearId && application.ApplicationNumber.StartsWith("APP-")
				select application.ApplicationNumber).ToListAsync();
			var highest = numbers.Select(ReadLegacySequence).DefaultIfEmpty().Max();
			next = Math.Max(next, checked(highest + 1));
		}
		else
		{
			if (rule.ResetPolicy == AdmissionNumberResetPolicy.CalendarYear)
			{
				var year = int.Parse(scopeKey["calendar:".Length..], System.Globalization.CultureInfo.InvariantCulture);
				legacy = legacy.Where(x => x.Year == year);
			}
			next = Math.Max(next, await legacy.Select(x => (long?)x.NextValue).MaxAsync() ?? 1);
		}
		if (counter?.LastRuleVersion != rule.Version || counter.LastYearText != yearText)
		{
			var prefix = rule.NumberPrefix(yearText);
			var candidates = await db.Set<AdmissionApplication>().IgnoreQueryFilters().AsNoTracking()
				.Where(x => x.TenantId == tenant.Id && x.ApplicationNumber.StartsWith(prefix)).Select(x => x.ApplicationNumber).ToListAsync();
			var highest = candidates.Select(x => rule.TryReadSequence(x, yearText, out var n) ? n : 0).DefaultIfEmpty().Max();
			next = Math.Max(next, checked(highest + 1));
		}
		return next;
	}
	private static long ReadLegacySequence(string number)
	{
		var parts = number.Split('-');
		return parts.Length == 3 && parts[0] == "APP" && parts[1].Length == 4 && parts[1].All(char.IsAsciiDigit) &&
			parts[2].All(char.IsAsciiDigit) && long.TryParse(parts[2], out var value) ? value : 0;
	}
}
