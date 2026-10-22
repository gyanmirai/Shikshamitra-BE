using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionApplicationNumberCounter : AggregateRoot<Guid>
{
	public Guid StoredTenantId { get; private set; }
	public string ScopeKey { get; private set; } = "";
	public long NextValue { get; private set; }
	public int LastRuleVersion { get; private set; }
	public string LastYearText { get; private set; } = "";
	private AdmissionApplicationNumberCounter() { }
	public AdmissionApplicationNumberCounter(Guid id, Guid? tenantId, string scopeKey, long startingNumber) : base(id)
	{ StoredTenantId = tenantId ?? Guid.Empty; ScopeKey = scopeKey; NextValue = startingNumber; }
	public void EnsureFloor(long floor, int version, string yearText) { NextValue = Math.Max(NextValue, floor); LastRuleVersion = version; LastYearText = yearText; }
	public long TakeNext() { var value = NextValue; NextValue = checked(NextValue + 1); return value; }
}
