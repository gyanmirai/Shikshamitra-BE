using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Shared;

public sealed class LibraryBorrowerScopeCapture : BasicAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public LibraryBorrowerScopeSourceKind SourceKind { get; private set; }
	public Guid SourceId { get; private set; }
	public Guid CanonicalEmployeeId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public int Version { get; private set; }
	public LibraryBorrowerScopeProvenance Provenance { get; private set; }
	public bool IsComplete { get; private set; }
	public int ExpectedAssignmentCount { get; private set; }
	public string? IncompleteReason { get; private set; }
	public string SourceName { get; private set; } = string.Empty;
	public DateTimeOffset CapturedAt { get; private set; }
	public LibraryActorKind ActorKind { get; private set; }
	public Guid? ActorId { get; private set; }
	public LibrarySystemPurpose? SystemPurpose { get; private set; }
	public string? AuditJson { get; private set; }
	private readonly List<LibraryBorrowerAssignmentSnapshot> _assignments = [];
	public IReadOnlyCollection<LibraryBorrowerAssignmentSnapshot> Assignments => _assignments;
	private LibraryBorrowerScopeCapture() { }

	public LibraryBorrowerScopeCapture(Guid id, Guid tenantId, LibraryBorrowerScopeSourceKind sourceKind, Guid sourceId,
		Guid employeeId, Guid academicYearId, int version, LibraryBorrowerScopeProvenance provenance, LibraryOperationActor actor,
		DateTimeOffset capturedAt, IReadOnlyCollection<LibraryBorrowerAssignmentEvidence> assignments, string? auditJson = null) : base(id)
	{
		if (id == Guid.Empty || tenantId == Guid.Empty || !Enum.IsDefined(sourceKind) || sourceId == Guid.Empty || employeeId == Guid.Empty ||
			academicYearId == Guid.Empty || version < 1 || provenance is not (LibraryBorrowerScopeProvenance.CapturedAtOperation or LibraryBorrowerScopeProvenance.ReviewedLegacy) ||
			actor == null || capturedAt == default || assignments == null || assignments.Select(row => row.OrganizationAssignmentId).Distinct().Count() != assignments.Count)
			throw new BusinessException(LibraryErrorCodes.ContextInvalid);
		if (auditJson != null)
		{
			if (auditJson.Length > 50000) throw new BusinessException(LibraryErrorCodes.ContextInvalid);
			try { using var json = JsonDocument.Parse(auditJson); if (json.RootElement.ValueKind != JsonValueKind.Object) throw new BusinessException(LibraryErrorCodes.ContextInvalid); }
			catch (JsonException) { throw new BusinessException(LibraryErrorCodes.ContextInvalid); }
		}
		TenantId = tenantId; SourceKind = sourceKind; SourceId = sourceId; CanonicalEmployeeId = employeeId; AcademicYearId = academicYearId;
		Version = version; Provenance = provenance; SourceName = "EmployeeOrganizationAssignments"; CapturedAt = capturedAt.ToUniversalTime();
		ActorKind = actor.Kind; ActorId = actor.UserId; SystemPurpose = actor.SystemPurpose; AuditJson = auditJson;
		ExpectedAssignmentCount = assignments.Count;
		foreach (var row in assignments) _assignments.Add(new(Guid.NewGuid(), tenantId, id, row));
		IsComplete = assignments.Count > 0 && assignments.All(row => row.IsComplete) && assignments.Any(row => row.AcademicYearId == academicYearId);
		IncompleteReason = IsComplete ? null : assignments.Count == 0 ? "No current organization assignments were available at capture." :
			assignments.Any(row => !row.IsComplete) ? "One or more recorded organization assignments have an unresolved academic tuple." :
			"No recorded assignment belongs to the transaction academic year.";
	}

	public void EnsureSource(Guid? tenantId, LibraryBorrowerScopeSourceKind kind, Guid sourceId, Guid memberId, Guid? yearId)
	{
		if (!tenantId.HasValue || tenantId != TenantId || SourceKind != kind || SourceId != sourceId || CanonicalEmployeeId != memberId || AcademicYearId != yearId)
			throw new BusinessException(LibraryErrorCodes.ContextInvalid);
	}
}
