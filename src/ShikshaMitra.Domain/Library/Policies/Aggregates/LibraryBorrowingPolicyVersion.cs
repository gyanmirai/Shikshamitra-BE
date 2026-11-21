using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Policies;

public class LibraryBorrowingPolicyVersion : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid OriginAcademicYearId { get; private set; }
	public Guid PolicyId { get; private set; }
	public int Version { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public int Role { get; private set; }
	public DateOnly EffectiveFrom { get; private set; }
	public DateOnly? EffectiveTo { get; private set; }
	public Guid? BookCategoryId { get; private set; }
	public Guid? SectionId { get; private set; }
	public Guid? EducationMediumStageId { get; private set; }
	public Guid? StandardId { get; private set; }
	public int Priority { get; private set; }
	public LibraryBorrowingTerms Terms { get; private set; } = null!;
	public LibraryBorrowingPolicyState Status { get; private set; }
	public DateTimeOffset? PublishedAt { get; private set; }
	public DateOnly? ArchivedFrom { get; private set; }
	public string? ArchiveReason { get; private set; }
	public int Specificity => (BookCategoryId.HasValue ? 1 : 0) + (SectionId.HasValue ? 1 : 0) +
		(EducationMediumStageId.HasValue ? 1 : 0) + (StandardId.HasValue ? 1 : 0);
	public DateOnly LastEffectiveDate => ArchivedFrom.HasValue
		? Min(EffectiveTo ?? DateOnly.MaxValue, ArchivedFrom.Value.AddDays(-1)) : EffectiveTo ?? DateOnly.MaxValue;

	protected LibraryBorrowingPolicyVersion() { }

	public LibraryBorrowingPolicyVersion(Guid id, Guid? tenantId, Guid originYearId, Guid policyId, int version,
		LibraryBorrowingPolicyDefinition definition) : base(id)
	{
		if (id == Guid.Empty || originYearId == Guid.Empty || policyId == Guid.Empty || version < 1)
			throw new BusinessException(LibraryErrorCodes.PolicyInvalid);
		TenantId = tenantId;
		OriginAcademicYearId = originYearId;
		PolicyId = policyId;
		Version = version;
		Apply(definition.Validated());
	}

	public void UpdateDraft(LibraryBorrowingPolicyDefinition definition, string? expectedStamp)
	{
		EnsureStamp(expectedStamp);
		if (Status != LibraryBorrowingPolicyState.Draft)
			throw new BusinessException(LibraryErrorCodes.PolicyImmutable, "Published policy terms cannot be edited. Create a new version instead.");
		Apply(definition.Validated());
	}

	public void Publish(string? expectedStamp, DateTimeOffset now)
	{
		EnsureStamp(expectedStamp);
		if (Status != LibraryBorrowingPolicyState.Draft) throw new BusinessException(LibraryErrorCodes.PolicyImmutable);
		Status = LibraryBorrowingPolicyState.Published;
		PublishedAt = now.ToUniversalTime();
	}

	public void Archive(DateOnly effectiveOn, string reason, string? expectedStamp)
	{
		EnsureStamp(expectedStamp);
		if (Status != LibraryBorrowingPolicyState.Published) throw new BusinessException(LibraryErrorCodes.PolicyImmutable);
		if (effectiveOn == default || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
			throw new BusinessException(LibraryErrorCodes.PolicyInvalid);
		Status = LibraryBorrowingPolicyState.Archived;
		ArchivedFrom = effectiveOn;
		ArchiveReason = reason.Trim();
	}

	public bool IsEffectiveOn(DateOnly date) => Status != LibraryBorrowingPolicyState.Draft &&
		date >= EffectiveFrom && date <= LastEffectiveDate;

	public bool Matches(LibraryBorrowingPolicyContext context) => Role == context.Role &&
		(!BookCategoryId.HasValue || BookCategoryId == context.BookCategoryId) &&
		(!SectionId.HasValue || SectionId == context.SectionId) &&
		(!EducationMediumStageId.HasValue || EducationMediumStageId == context.EducationMediumStageId) &&
		(!StandardId.HasValue || StandardId == context.StandardId);

	public bool ConflictsWith(LibraryBorrowingPolicyVersion other) =>
		Role == other.Role && LastEffectiveDate >= EffectiveFrom && other.LastEffectiveDate >= other.EffectiveFrom &&
		EffectiveFrom <= other.LastEffectiveDate && other.EffectiveFrom <= LastEffectiveDate &&
		(PolicyId == other.PolicyId || Specificity == other.Specificity && Priority == other.Priority &&
			Intersects(BookCategoryId, other.BookCategoryId) && Intersects(SectionId, other.SectionId) &&
			Intersects(EducationMediumStageId, other.EducationMediumStageId) && Intersects(StandardId, other.StandardId));

	private void Apply(LibraryBorrowingPolicyDefinition definition)
	{
		Name = definition.Name; Role = definition.Role; EffectiveFrom = definition.EffectiveFrom; EffectiveTo = definition.EffectiveTo;
		BookCategoryId = definition.BookCategoryId; SectionId = definition.SectionId;
		EducationMediumStageId = definition.EducationMediumStageId; StandardId = definition.StandardId;
		Priority = definition.Priority; Terms = definition.Terms;
	}

	private void EnsureStamp(string? expectedStamp)
	{
		if (string.IsNullOrWhiteSpace(expectedStamp) || !string.Equals(ConcurrencyStamp, expectedStamp, StringComparison.Ordinal))
			throw new BusinessException(LibraryErrorCodes.PolicyConflict, "The policy changed. Reload it before saving.");
	}
	private static bool Intersects(Guid? left, Guid? right) => !left.HasValue || !right.HasValue || left == right;
	private static DateOnly Min(DateOnly left, DateOnly right) => left < right ? left : right;
}
