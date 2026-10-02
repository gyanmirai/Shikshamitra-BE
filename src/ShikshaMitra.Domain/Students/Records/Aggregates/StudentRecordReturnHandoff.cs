using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using ShikshaMitra.Students.StudentEnrollments.Admissions;

namespace ShikshaMitra.Students.Records;

public sealed class StudentRecordReturnHandoff : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid? AdmissionApplicationId { get; private set; }
	public Guid? AdmissionInquiryId { get; private set; }
	public Guid? TransitionId { get; private set; }
	public AdmissionEntryMode? EntryPath { get; private set; }
	public Guid? AdmissionOfferingId { get; private set; }
	public string SourceSnapshotJson { get; private set; } = "{}";
	public string Route { get; private set; } = "";
	public string IdempotencyKey { get; private set; } = "";
	public string RequestJson { get; private set; } = "{}";
	public string PolicyJson { get; private set; } = "{}";
	private StudentRecordReturnHandoff() { }
	public StudentRecordReturnHandoff(Guid id, Guid tenantId, Guid studentId, Guid yearId, Guid applicationId,
		string route, string key, string requestJson, string policyJson) : base(id)
	{
		TenantId = Check.NotDefaultOrNull<Guid>(tenantId, nameof(tenantId)); StudentId = studentId; AcademicYearId = yearId;
		AdmissionApplicationId = applicationId; Route = route; IdempotencyKey = Check.NotNullOrWhiteSpace(key, nameof(key), 128);
		RequestJson = requestJson; PolicyJson = policyJson;
	}
	public StudentRecordReturnHandoff(Guid id, Guid tenantId, Guid studentId, Guid yearId, Guid transitionId,
		Guid offeringId, AdmissionEntryMode? entryPath, string route, string key, string requestJson, string policyJson,
		string sourceSnapshotJson) : base(id)
	{
		TenantId = Check.NotDefaultOrNull<Guid>(tenantId, nameof(tenantId)); StudentId = Check.NotDefaultOrNull<Guid>(studentId, nameof(studentId));
		AcademicYearId = Check.NotDefaultOrNull<Guid>(yearId, nameof(yearId)); TransitionId = Check.NotDefaultOrNull<Guid>(transitionId, nameof(transitionId));
		AdmissionOfferingId = Check.NotDefaultOrNull<Guid>(offeringId, nameof(offeringId));
		if (entryPath.HasValue && entryPath is not (AdmissionEntryMode.Inquiry or AdmissionEntryMode.DirectRegistration)) throw new BusinessException("Students:Records:ReturnEntryPathRequired");
		EntryPath = entryPath; Route = Check.NotNullOrWhiteSpace(route, nameof(route), 32); IdempotencyKey = Check.NotNullOrWhiteSpace(key, nameof(key), 128);
		RequestJson = requestJson; PolicyJson = policyJson; SourceSnapshotJson = sourceSnapshotJson;
	}
	public void LinkInquiry(Guid id)
	{
		if (EntryPath != AdmissionEntryMode.Inquiry || AdmissionInquiryId.HasValue && AdmissionInquiryId != id) throw new BusinessException("Students:Records:IdempotencyConflict");
		AdmissionInquiryId = Check.NotDefaultOrNull<Guid>(id, nameof(id));
	}
	public void LinkApplication(Guid id)
	{
		if (AdmissionApplicationId.HasValue && AdmissionApplicationId != id) throw new BusinessException("Students:Records:IdempotencyConflict");
		AdmissionApplicationId = Check.NotDefaultOrNull<Guid>(id, nameof(id));
	}
}
