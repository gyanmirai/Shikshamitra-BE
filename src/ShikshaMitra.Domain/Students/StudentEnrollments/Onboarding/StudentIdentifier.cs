using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public class StudentIdentifierSeries : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid? StandardId { get; private set; }
	public StudentIdentifierType IdentifierType { get; private set; }
	public string Code { get; private set; } = string.Empty;
	public string Name { get; private set; } = string.Empty;
	public string Prefix { get; private set; } = string.Empty;
	public string Suffix { get; private set; } = string.Empty;
	public int Padding { get; private set; }
	public long NextSequence { get; private set; }
	public long? MaximumSequence { get; private set; }
	public DateTime EffectiveFrom { get; private set; }
	public DateTime? EffectiveUntil { get; private set; }
	public bool IsActive { get; private set; }

	protected StudentIdentifierSeries() { }

	public StudentIdentifierSeries(Guid id, Guid? tenantId, Guid academicYearId, Guid? standardId,
		StudentIdentifierType identifierType, string code, string name, string prefix, string suffix,
		int padding, long startingSequence, long? maximumSequence, DateTime effectiveFrom, DateTime? effectiveUntil) : base(id)
	{
		if (padding is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(padding));
		if (startingSequence <= 0 || maximumSequence.HasValue && maximumSequence < startingSequence)
			throw new ArgumentOutOfRangeException(nameof(startingSequence));
		if (effectiveUntil.HasValue && effectiveUntil.Value <= effectiveFrom)
			throw new ArgumentException("Effective-until must be after effective-from.", nameof(effectiveUntil));
		TenantId = tenantId;
		AcademicYearId = Check.NotDefaultOrNull<Guid>(academicYearId, nameof(academicYearId));
		StandardId = standardId;
		IdentifierType = identifierType;
		Code = Check.NotNullOrWhiteSpace(code, nameof(code), 50).Trim().ToUpperInvariant();
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), 120).Trim();
		Prefix = NormalizeAffix(prefix, nameof(prefix));
		Suffix = NormalizeAffix(suffix, nameof(suffix));
		Padding = padding;
		NextSequence = startingSequence;
		MaximumSequence = maximumSequence;
		EffectiveFrom = effectiveFrom.ToUniversalTime();
		EffectiveUntil = effectiveUntil?.ToUniversalTime();
		IsActive = true;
	}

	public (long Sequence, string Value) Allocate(DateTime at)
	{
		EnsureAvailable(at);
		var sequence = NextSequence++;
		return (sequence, $"{Prefix}{sequence.ToString().PadLeft(Padding, '0')}{Suffix}");
	}

	public void EnsureAvailable(DateTime at)
	{
		var now = at.ToUniversalTime();
		if (!IsActive || now < EffectiveFrom || EffectiveUntil.HasValue && now > EffectiveUntil.Value ||
		    MaximumSequence.HasValue && NextSequence > MaximumSequence.Value)
			throw new BusinessException(StudentOnboardingErrorCodes.IdentifierSeriesUnavailable);
	}

	public void Deactivate() => IsActive = false;

	public void AdvancePastManualSequence(long sequence)
	{
		if (sequence < 1 || sequence == long.MaxValue || MaximumSequence.HasValue && sequence > MaximumSequence.Value)
			throw new BusinessException(StudentOnboardingErrorCodes.IdentifierSeriesUnavailable);
		NextSequence = Math.Max(NextSequence, sequence + 1);
	}

	public void ApplyRule(string name, string prefix, string suffix, int padding, long startingNumber, long? maximumNumber)
	{
		if (padding is < 1 or > 20 || startingNumber < 1) throw new ArgumentOutOfRangeException(nameof(padding));
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), 120).Trim();
		Prefix = NormalizeAffix(prefix, nameof(prefix));
		Suffix = NormalizeAffix(suffix, nameof(suffix));
		Padding = padding;
		// Editing a rule must never rewind a committed or reserved sequence.
		NextSequence = Math.Max(NextSequence, startingNumber);
		MaximumSequence = maximumNumber;
	}

	private static string NormalizeAffix(string? value, string parameterName)
	{
		var normalized = value?.Trim() ?? string.Empty;
		if (normalized.Length > 40) throw new ArgumentOutOfRangeException(parameterName);
		return normalized;
	}
}

public class StudentIdentifierAllocation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid OnboardingCaseId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid NumberSeriesId { get; private set; }
	public StudentIdentifierType IdentifierType { get; private set; }
	public long Sequence { get; private set; }
	public string Value { get; private set; } = string.Empty;
	public StudentIdentifierAllocationStatus Status { get; private set; }
	public bool IsManual { get; private set; }
	public string? Reason { get; private set; }
	public DateTime AllocatedAt { get; private set; }
	public DateTime? CommittedAt { get; private set; }

	protected StudentIdentifierAllocation() { }

	public StudentIdentifierAllocation(Guid id, Guid? tenantId, Guid onboardingCaseId, Guid studentId,
		Guid numberSeriesId, StudentIdentifierType identifierType, long sequence, string value,
		DateTime allocatedAt, bool isManual = false, string? reason = null) : base(id)
	{
		TenantId = tenantId;
		OnboardingCaseId = Check.NotDefaultOrNull<Guid>(onboardingCaseId, nameof(onboardingCaseId));
		StudentId = Check.NotDefaultOrNull<Guid>(studentId, nameof(studentId));
		NumberSeriesId = Check.NotDefaultOrNull<Guid>(numberSeriesId, nameof(numberSeriesId));
		IdentifierType = identifierType;
		Sequence = sequence;
		Value = Check.NotNullOrWhiteSpace(value, nameof(value), 100).Trim();
		AllocatedAt = allocatedAt.ToUniversalTime();
		IsManual = isManual;
		Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
		Status = StudentIdentifierAllocationStatus.Reserved;
	}

	public void Commit(DateTime at)
	{
		if (Status == StudentIdentifierAllocationStatus.Committed) return;
		if (Status != StudentIdentifierAllocationStatus.Reserved)
			throw new BusinessException(StudentOnboardingErrorCodes.IdentifierAlreadyAllocated);
		Status = StudentIdentifierAllocationStatus.Committed;
		CommittedAt = at.ToUniversalTime();
	}

	public void CorrectReservation(Guid seriesId, long sequence, string value, string reason, DateTime at)
	{
		if (Status != StudentIdentifierAllocationStatus.Reserved) throw new BusinessException(StudentOnboardingErrorCodes.CaseNotEditable);
		Reason = Check.NotNullOrWhiteSpace(reason, nameof(reason), 2000).Trim();
		NumberSeriesId = seriesId; Sequence = sequence;
		Value = Check.NotNullOrWhiteSpace(value, nameof(value), 100).Trim();
		IsManual = true; AllocatedAt = at.ToUniversalTime();
	}

	public void Release()
	{
		if (Status == StudentIdentifierAllocationStatus.Reserved)
			Status = StudentIdentifierAllocationStatus.Released;
	}
}
