using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmNumberSeries : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string Code { get; private set; } = string.Empty;
	public string Name { get; private set; } = string.Empty;
	public AdmissionCrmIdentifierType IdentifierType { get; private set; }
	public Guid? EducationBoardId { get; private set; }
	public Guid? AcademicMediumId { get; private set; }
	public Guid? StandardId { get; private set; }
	public string Prefix { get; private set; } = string.Empty;
	public string Suffix { get; private set; } = string.Empty;
	public int Padding { get; private set; }
	public long StartingSequence { get; private set; }
	public long NextSequence { get; private set; }
	public long? MaximumSequence { get; private set; }
	public DateTime EffectiveFrom { get; private set; }
	public DateTime? EffectiveUntil { get; private set; }
	public AdmissionCrmNumberSeriesStatus Status { get; private set; }
	public Guid? SubmittedByUserId { get; private set; }
	public Guid? ApprovedByUserId { get; private set; }

	protected AdmissionCrmNumberSeries() { }

	public AdmissionCrmNumberSeries(Guid id, Guid? tenantId, Guid academicYearId, string code,
		string name, AdmissionCrmIdentifierType identifierType, Guid? educationBoardId,
		Guid? academicMediumId, Guid? standardId, string prefix, string suffix, int padding,
		long startingSequence, long? maximumSequence, DateTime effectiveFrom, DateTime? effectiveUntil) : base(id)
	{
		if (padding is < 1 or > 12 || startingSequence <= 0 || maximumSequence < startingSequence)
			throw new BusinessException("AdmissionCrm:InvalidNumberSeries");
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		Code = Check.NotNullOrWhiteSpace(code, nameof(code), AdmissionCrmConsts.MaxPolicyCodeLength).ToUpperInvariant();
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), AdmissionCrmConsts.MaxNameLength);
		IdentifierType = identifierType;
		EducationBoardId = educationBoardId;
		AcademicMediumId = academicMediumId;
		StandardId = standardId;
		Prefix = Check.Length(prefix ?? string.Empty, nameof(prefix), AdmissionCrmConsts.MaxNumberPatternLength);
		Suffix = Check.Length(suffix ?? string.Empty, nameof(suffix), AdmissionCrmConsts.MaxNumberPatternLength);
		Padding = padding;
		StartingSequence = startingSequence;
		NextSequence = startingSequence;
		MaximumSequence = maximumSequence;
		EffectiveFrom = effectiveFrom.ToUniversalTime();
		EffectiveUntil = effectiveUntil?.ToUniversalTime();
		if (EffectiveUntil.HasValue && EffectiveUntil <= EffectiveFrom)
			throw new BusinessException("AdmissionCrm:InvalidNumberSeriesPeriod");
		Status = AdmissionCrmNumberSeriesStatus.Draft;
	}

	public void Submit(Guid userId)
	{
		if (Status != AdmissionCrmNumberSeriesStatus.Draft) throw new BusinessException("AdmissionCrm:NumberSeriesNotDraft");
		SubmittedByUserId = userId;
		Status = AdmissionCrmNumberSeriesStatus.ApprovalPending;
	}

	public void Publish(Guid userId)
	{
		if (Status != AdmissionCrmNumberSeriesStatus.ApprovalPending) throw new BusinessException("AdmissionCrm:NumberSeriesNotAwaitingApproval");
		if (SubmittedByUserId == userId) throw new BusinessException("AdmissionCrm:DistinctNumberSeriesApproverRequired");
		ApprovedByUserId = userId;
		Status = AdmissionCrmNumberSeriesStatus.Published;
	}

	public (long Sequence, string Value) Allocate(DateTime at)
	{
		var now = at.ToUniversalTime();
		if (Status != AdmissionCrmNumberSeriesStatus.Published || now < EffectiveFrom || EffectiveUntil.HasValue && now > EffectiveUntil)
			throw new BusinessException("AdmissionCrm:NumberSeriesNotActive");
		if (MaximumSequence.HasValue && NextSequence > MaximumSequence)
			throw new BusinessException("AdmissionCrm:NumberSeriesExhausted");
		var sequence = NextSequence++;
		return (sequence, $"{Prefix}{sequence.ToString().PadLeft(Padding, '0')}{Suffix}");
	}
}
