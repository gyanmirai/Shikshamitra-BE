using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.InitialSetup;

/// <summary>One durable setup workflow per school; the tenant ID is also its primary key.</summary>
public class InstituteSetupProgress : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public int CurrentStep { get; private set; }
	public int FurthestStep { get; private set; }
	public string DraftJson { get; private set; } = "{}";
	public Guid? AcademicYearId { get; private set; }
	public Guid? DivisionId { get; private set; }
	public DateTime? CompletedAt { get; private set; }
	public Guid? CompletedBy { get; private set; }
	private InstituteSetupProgress() { }
	public InstituteSetupProgress(Guid tenantId) : base(tenantId) => TenantId = tenantId;

	public void SaveDraft(int step, string draft)
	{
		EnsureIncomplete();
		if (step < 0 || step > FurthestStep) throw new BusinessException(InitialSetupErrorCodes.EarlierStepsRequired);
		CurrentStep = step;
		DraftJson = Check.NotNullOrWhiteSpace(draft, nameof(draft), maxLength: 16384);
	}
	public void Advance(int fromStep)
	{
		EnsureIncomplete();
		if (fromStep < 0 || fromStep > FurthestStep || fromStep > 4)
			throw new BusinessException(InitialSetupErrorCodes.EarlierStepsRequired);
		FurthestStep = Math.Max(FurthestStep, fromStep + 1);
		CurrentStep = fromStep + 1;
	}
	public void SelectYear(Guid yearId)
	{
		EnsureIncomplete();
		if (AcademicYearId != yearId)
		{
			DivisionId = null;
			FurthestStep = Math.Min(FurthestStep, 3);
		}
		AcademicYearId = yearId;
	}
	public void SelectDivision(Guid divisionId) { EnsureIncomplete(); DivisionId = divisionId; }
	public void Complete(DateTime now, Guid userId)
	{
		if (CompletedAt.HasValue) return;
		if (FurthestStep < 5) throw new BusinessException(InitialSetupErrorCodes.ReviewRequired);
		CompletedAt = now;
		CompletedBy = userId;
		CurrentStep = FurthestStep = 6;
	}
	public void PreserveLegacyCompletion(DateTime now)
	{
		CompletedAt = now;
		CurrentStep = FurthestStep = 6;
	}
	private void EnsureIncomplete()
	{
		if (CompletedAt.HasValue) throw new BusinessException(InitialSetupErrorCodes.AlreadyCompleted);
	}
}
