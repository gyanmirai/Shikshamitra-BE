using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmIdentifierWorkflow(
	IRepository<AdmissionCrmIdentifierAllocation, Guid> identifierRepository,
	IRepository<AdmissionCrmNumberSeries, Guid> seriesRepository,
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	AdmissionCrmActivationAccess access,
	AdmissionCrmActivationTimeline timeline,
	StudentScopeAccessService scopeAccess) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmIdentifierAllocationDto> AssignManualAsync(
		Guid activationId,
		AssignManualAdmissionCrmIdentifierDto input)
	{
		var activation = await access.GetActivationAsync(activationId, "AssignManualIdentifierAsync");
		if (activation.StudentId.HasValue || activation.Status is AdmissionCrmActivationStatus.InProgress or
		    AdmissionCrmActivationStatus.FirstDayReady or AdmissionCrmActivationStatus.Cancelled or
		    AdmissionCrmActivationStatus.CancellationPending)
			throw new BusinessException("AdmissionCrm:ManualIdentifierAssignmentClosed");
		if (input.Sequence <= 0 || input.Sequence > int.MaxValue)
			throw new BusinessException("AdmissionCrm:InvalidManualIdentifierSequence");
		var series = await access.GetSeriesAsync(input.NumberSeriesId);
		if (series.Id != activation.GrNumberSeriesId || series.IdentifierType != AdmissionCrmIdentifierType.GrNumber ||
		    series.Status != AdmissionCrmNumberSeriesStatus.Published)
			throw new BusinessException("AdmissionCrm:ActivationNumberSeriesMismatch");
		var query = await identifierRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(query.Where(x => x.TenantId == CurrentTenant.Id &&
		    (x.ActivationId == activation.Id && x.IdentifierType == AdmissionCrmIdentifierType.GrNumber ||
		     x.IdentifierType == AdmissionCrmIdentifierType.GrNumber && x.Value == input.Value ||
		     x.NumberSeriesId == series.Id && x.Sequence == input.Sequence))))
			throw new BusinessException("AdmissionCrm:IdentifierAlreadyAllocated");
		var allocation = new AdmissionCrmIdentifierAllocation(GuidGenerator.Create(), CurrentTenant.Id,
			activation.AcademicYearId, activation.LeadId, activation.Id, series.Id,
			AdmissionCrmIdentifierType.GrNumber, input.Sequence, input.Value, Clock.Now);
		allocation.MarkManual(GetUserId(), input.Reason);
		await identifierRepository.InsertAsync(allocation, autoSave: true);
		var lead = await access.GetLeadAsync(activation.LeadId, "AssignManualIdentifierAsync");
		await timeline.AddAsync(lead, AdmissionCrmTimelineEventType.IdentifierAllocated,
			"GR number manually assigned", $"{input.Value}. Reason: {input.Reason}", allocation.Id);
		return AdmissionCrmActivationMapper.Identifier(allocation);
	}

	public async Task<AdmissionCrmIdentifierGovernanceReportDto> GetGovernanceAsync(Guid academicYearId, Guid seriesId)
	{
		await scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, "GetIdentifierGovernanceReportAsync");
		var series = await access.GetSeriesAsync(seriesId);
		if (series.AcademicYearId != academicYearId)
			throw new BusinessException("AdmissionCrm:NumberSeriesAcademicYearMismatch");
		var query = await identifierRepository.GetQueryableAsync();
		var all = await AsyncExecuter.ToListAsync(query.Where(x =>
			x.TenantId == CurrentTenant.Id && x.NumberSeriesId == seriesId));
		var leadQuery = await leadRepository.GetQueryableAsync();
		var scoped = leadQuery.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId);
		scoped = await scopeAccess.ApplyCurrentUserAdmissionLeadScopeAsync(scoped);
		var leadIds = (await AsyncExecuter.ToListAsync(scoped.Select(x => x.Id))).ToHashSet();
		var allocations = all.Where(x => leadIds.Contains(x.LeadId)).ToList();
		var used = all.Select(x => x.Sequence).ToHashSet();
		var gapCount = Math.Max(0, series.NextSequence - series.StartingSequence -
			used.Count(x => x >= series.StartingSequence && x < series.NextSequence));
		var gaps = Enumerable.Range(0, (int)Math.Min(series.NextSequence - series.StartingSequence, 100_000))
			.Select(offset => series.StartingSequence + offset).Where(x => !used.Contains(x)).Take(500).ToList();
		return new AdmissionCrmIdentifierGovernanceReportDto
		{
			AcademicYearId = academicYearId, NumberSeriesId = series.Id, SeriesCode = series.Code,
			StartingSequence = series.StartingSequence, NextSequence = series.NextSequence,
			MaximumSequence = series.MaximumSequence, AllocatedCount = allocations.Count,
			CommittedCount = allocations.Count(x => x.Status == AdmissionCrmIdentifierStatus.Committed),
			ManualCount = allocations.Count(x => x.IsManual),
			VoidedCount = allocations.Count(x => x.Status == AdmissionCrmIdentifierStatus.Void),
			IsExhausted = series.MaximumSequence.HasValue && series.NextSequence > series.MaximumSequence.Value,
			GapSequences = gaps, GapReportTruncated = gapCount > gaps.Count,
			Allocations = allocations.OrderBy(x => x.Sequence).Select(AdmissionCrmActivationMapper.Identifier).ToList()
		};
	}

	public async Task<AdmissionCrmIdentifierAllocation> GetOrAllocateAsync(
		AdmissionCrmActivation activation,
		AdmissionInquiryLead lead,
		List<AdmissionCrmActivationStepDto> steps)
	{
		var query = await identifierRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x =>
			x.TenantId == CurrentTenant.Id && x.ActivationId == activation.Id &&
			x.IdentifierType == AdmissionCrmIdentifierType.GrNumber));
		if (existing != null) return existing;
		var series = await access.GetSeriesAsync(activation.GrNumberSeriesId);
		var value = series.Allocate(Clock.Now);
		await seriesRepository.UpdateAsync(series, autoSave: true);
		var allocation = new AdmissionCrmIdentifierAllocation(GuidGenerator.Create(), CurrentTenant.Id,
			lead.AcademicYearId, lead.Id, activation.Id, series.Id, AdmissionCrmIdentifierType.GrNumber,
			value.Sequence, value.Value, Clock.Now);
		await identifierRepository.InsertAsync(allocation, autoSave: true);
		AdmissionCrmActivationMapper.UpsertStep(steps, "gr_number", AdmissionCrmActivationStepStatus.Completed,
			$"Allocated {value.Value}.");
		await timeline.AddAsync(lead, AdmissionCrmTimelineEventType.IdentifierAllocated,
			"GR number allocated", value.Value, allocation.Id);
		return allocation;
	}

	private Guid GetUserId() => CurrentUser.Id ?? throw new AbpAuthorizationException("An authenticated user is required.");
}
