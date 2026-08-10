using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.BulkOperations)]
[RemoteService(false)]
public class AdmissionCrmBulkOperationsAppService : ApplicationService, IAdmissionCrmBulkOperationsAppService
{
	private const int MaximumBatchSize = 100;
	private readonly IAdmissionCrmLeadAppService _leadService;
	private readonly IAdmissionCrmFinalAdmissionAppService _finalAdmissionService;
	private readonly AdmissionCrmActivationBulkWorkflow _activationWorkflow;
	private readonly AdmissionCrmEntranceExamBulkWorkflow _examBulkWorkflow;
	private readonly AdmissionCrmMessageDeliveryWorkflow _communicationWorkflow;
	private readonly IRepository<AdmissionCrmActivation, Guid> _activationRepository;
	private readonly IRepository<AdmissionCrmEntranceExamRecord, Guid> _entranceExamRepository;
	private readonly IRepository<AdmissionInquiryLead, Guid> _leadRepository;
	private readonly StudentScopeAccessService _scopeAccess;

	public AdmissionCrmBulkOperationsAppService(
		IAdmissionCrmLeadAppService leadService,
		IAdmissionCrmFinalAdmissionAppService finalAdmissionService,
		AdmissionCrmActivationBulkWorkflow activationWorkflow,
		AdmissionCrmEntranceExamBulkWorkflow examBulkWorkflow,
		AdmissionCrmMessageDeliveryWorkflow communicationWorkflow,
		IRepository<AdmissionCrmActivation, Guid> activationRepository,
		IRepository<AdmissionCrmEntranceExamRecord, Guid> entranceExamRepository,
		IRepository<AdmissionInquiryLead, Guid> leadRepository,
		StudentScopeAccessService scopeAccess)
	{
		_leadService = leadService;
		_finalAdmissionService = finalAdmissionService;
		_activationWorkflow = activationWorkflow;
		_examBulkWorkflow = examBulkWorkflow;
		_communicationWorkflow = communicationWorkflow;
		_activationRepository = activationRepository;
		_entranceExamRepository = entranceExamRepository;
		_leadRepository = leadRepository;
		_scopeAccess = scopeAccess;
	}

	[UnitOfWork(IsDisabled = true)]
	public async Task<AdmissionCrmBulkOperationResultDto> AssignOwnersAsync(Guid academicYearId, AdmissionCrmBulkAssignOwnersDto input)
	{
		await EnsureBatchAsync(academicYearId, input.Items.Select(x => x.LeadId), nameof(AssignOwnersAsync));
		return await RunAsync(input.Items, x => x.LeadId, async item =>
		{
			var result = await _leadService.AssignOwnerAsync(item.LeadId, new AssignAdmissionCrmLeadOwnerDto { OwnerUserId = item.OwnerUserId });
			return result.Id.ToString();
		});
	}

	[UnitOfWork(IsDisabled = true)]
	public async Task<AdmissionCrmBulkOperationResultDto> CreateTasksAsync(Guid academicYearId, AdmissionCrmBulkCreateTasksDto input)
	{
		await EnsureBatchAsync(academicYearId, input.LeadIds, nameof(CreateTasksAsync));
		return await RunAsync(input.LeadIds.Distinct().ToList(), x => x, async leadId =>
		{
			var result = await _leadService.CreateTaskAsync(leadId, new CreateAdmissionCrmFollowUpTaskDto
			{
				Title = input.Title, DueAt = input.DueAt, AssignedUserId = input.AssignedUserId, Notes = input.Notes
			});
			return result.Id.ToString();
		});
	}

	[UnitOfWork(IsDisabled = true)]
	public async Task<AdmissionCrmBulkOperationResultDto> MarkExamAttendanceAsync(
		Guid academicYearId,
		AdmissionCrmBulkExamAttendanceDto input)
	{
		await EnsureBatchAsync(academicYearId, input.ExamRecordIds, nameof(MarkExamAttendanceAsync));
		if (!Enum.IsDefined(input.AttendanceStatus) || input.AttendanceStatus == AdmissionCrmExamAttendanceStatus.NotMarked)
			throw new BusinessException("AdmissionCrm:InvalidBulkExamAttendanceStatus");
		return await RunAsync(input.ExamRecordIds.Distinct().ToList(), x => x, async examRecordId =>
		{
			var exam = await _entranceExamRepository.GetAsync(examRecordId);
			if (exam.AcademicYearId != academicYearId)
				throw new BusinessException("AdmissionCrm:BulkAcademicYearMismatch");
			var result = await _examBulkWorkflow.MarkAttendanceAsync(
				examRecordId,
				new MarkAdmissionCrmExamAttendanceDto { AttendanceStatus = input.AttendanceStatus });
			return result.Id.ToString();
		});
	}

	[UnitOfWork(IsDisabled = true)]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeExamResult)]
	public async Task<AdmissionCrmBulkOperationResultDto> UploadExamResultsAsync(
		Guid academicYearId,
		AdmissionCrmBulkExamResultsDto input)
	{
		var duplicateExamRecordIds = input.Items
			.GroupBy(x => x.ExamRecordId)
			.Where(x => x.Count() > 1)
			.Select(x => x.Key)
			.OrderBy(x => x)
			.ToList();
		if (duplicateExamRecordIds.Count > 0)
		{
			throw new BusinessException("AdmissionCrm:DuplicateBulkExamResultItems")
				.WithData("ExamRecordIds", string.Join(",", duplicateExamRecordIds));
		}

		var items = input.Items.ToList();
		await EnsureBatchAsync(academicYearId, items.Select(x => x.ExamRecordId), nameof(UploadExamResultsAsync));
		return await RunAsync(items, x => x.ExamRecordId, async item =>
		{
			var exam = await _entranceExamRepository.GetAsync(item.ExamRecordId);
			if (exam.AcademicYearId != academicYearId)
				throw new BusinessException("AdmissionCrm:BulkAcademicYearMismatch");
			var result = await _examBulkWorkflow.UploadResultAsync(
				item.ExamRecordId,
				new UploadAdmissionCrmEntranceExamResultDto
				{
					TotalMarks = item.TotalMarks,
					PassingMarks = item.PassingMarks,
					ObtainedMarks = item.ObtainedMarks,
					MeritRank = item.MeritRank
				},
				item.CorrectionReason);
			return result.Id.ToString();
		});
	}

	[UnitOfWork(IsDisabled = true)]
	public async Task<AdmissionCrmBulkOperationResultDto> ChangeStageAsync(Guid academicYearId, AdmissionCrmBulkChangeStageDto input)
	{
		await EnsureBatchAsync(academicYearId, input.LeadIds, nameof(ChangeStageAsync));
		if (input.OverrideProtectedStages && string.IsNullOrWhiteSpace(input.Reason))
		{
			throw new BusinessException("AdmissionCrm:BulkProtectedStageOverrideReasonRequired");
		}
		return await RunAsync(input.LeadIds.Distinct().ToList(), x => x, async leadId =>
		{
			var lead = await _leadRepository.GetAsync(leadId);
			if (lead.TenantId != CurrentTenant.Id || lead.AcademicYearId != academicYearId)
			{
				throw new BusinessException("AdmissionCrm:BulkAcademicYearMismatch");
			}
			await _scopeAccess.EnsureCanAccessAdmissionLeadAsync(lead, nameof(ChangeStageAsync));
			if (!input.OverrideProtectedStages && IsProtectedBulkStage(lead.Stage))
			{
				throw new BusinessException("AdmissionCrm:BulkProtectedStage")
					.WithData("Stage", lead.Stage);
			}
			var result = await _leadService.ChangeStageAsync(
				leadId,
				new ChangeAdmissionCrmLeadStageDto
				{
					Stage = input.Stage,
					Reason = input.Reason,
					OverrideWorkflowGuards = input.OverrideProtectedStages
				});
			return result.Id.ToString();
		});
	}

	[UnitOfWork(IsDisabled = true)]
	public async Task<AdmissionCrmBulkOperationResultDto> ArchiveAsync(Guid academicYearId, AdmissionCrmBulkArchiveDto input)
	{
		await EnsureBatchAsync(academicYearId, input.LeadIds, nameof(ArchiveAsync));
		return await RunAsync(input.LeadIds.Distinct().ToList(), x => x, async leadId =>
		{
			var result = await _leadService.ArchiveAsync(leadId, new ArchiveAdmissionCrmLeadDto { Reason = input.Reason });
			return result.Id.ToString();
		});
	}

	[UnitOfWork(IsDisabled = true)]
	public async Task<AdmissionCrmBulkOperationResultDto> SendMessagesAsync(Guid academicYearId, AdmissionCrmBulkSendMessageDto input)
	{
		await EnsureBatchAsync(academicYearId, input.LeadIds, nameof(SendMessagesAsync));
		return await RunAsync(input.LeadIds.Distinct().ToList(), x => x, async leadId =>
		{
			var result = await _communicationWorkflow.SendAsync(leadId, new SendAdmissionCrmMessageTemplateDto
			{
				TemplateId = input.TemplateId, Variables = input.Variables
			});
			return result.Id.ToString();
		});
	}

	[UnitOfWork(IsDisabled = true)]
	public async Task<AdmissionCrmBulkOperationResultDto> PreviewOffersAsync(Guid academicYearId, AdmissionCrmBulkOfferDto input)
	{
		await EnsureBatchAsync(academicYearId, input.LeadIds, nameof(PreviewOffersAsync));
		var items = new List<AdmissionCrmBulkOperationItemResultDto>();
		foreach (var leadId in input.LeadIds.Distinct())
		{
			try
			{
				var preview = await _finalAdmissionService.PreviewOfferAsync(leadId, input.Offer);
				items.Add(new AdmissionCrmBulkOperationItemResultDto
				{
					EntityId = leadId, Success = preview.IsReady,
					ErrorCode = preview.IsReady ? null : "AdmissionCrm:BulkOfferNotReady",
					BlockingReasons = preview.BlockingReasons
				});
			}
			catch (Exception exception)
			{
				items.Add(Failure(leadId, exception));
			}
		}
		return Summarize(items);
	}

	[UnitOfWork(IsDisabled = true)]
	public async Task<AdmissionCrmBulkOperationResultDto> CreateOffersAsync(Guid academicYearId, AdmissionCrmBulkOfferDto input)
	{
		await EnsureBatchAsync(academicYearId, input.LeadIds, nameof(CreateOffersAsync));
		return await RunAsync(input.LeadIds.Distinct().ToList(), x => x, async leadId =>
		{
			var preview = await _finalAdmissionService.PreviewOfferAsync(leadId, input.Offer);
			if (!preview.IsReady)
			{
				throw new BusinessException("AdmissionCrm:BulkOfferNotReady")
					.WithData("BlockingReasons", string.Join(",", preview.BlockingReasons));
			}
			var result = await _finalAdmissionService.CreateOfferAsync(leadId, input.Offer);
			return result.Id.ToString();
		});
	}

	[UnitOfWork(IsDisabled = true)]
	public async Task<AdmissionCrmBulkOperationResultDto> PreviewActivationsAsync(Guid academicYearId, AdmissionCrmBulkActivationPreviewDto input)
	{
		await EnsureBatchAsync(academicYearId, input.LeadIds, nameof(PreviewActivationsAsync));
		var items = new List<AdmissionCrmBulkOperationItemResultDto>();
		foreach (var leadId in input.LeadIds.Distinct())
		{
			try
			{
				var preview = await _activationWorkflow.PreviewAsync(leadId, input.Activation);
				var blockers = preview.Items.Where(x => x.Status == AdmissionCrmActivationStepStatus.Failed)
					.Select(x => $"{x.Key}:{x.Detail}").ToList();
				items.Add(new AdmissionCrmBulkOperationItemResultDto
				{
					EntityId = leadId, Success = preview.IsReady,
					ErrorCode = preview.IsReady ? null : "AdmissionCrm:BulkActivationNotReady",
					BlockingReasons = blockers
				});
			}
			catch (Exception exception)
			{
				items.Add(Failure(leadId, exception));
			}
		}
		return Summarize(items);
	}

	[UnitOfWork(IsDisabled = true)]
	public async Task<AdmissionCrmBulkOperationResultDto> ExecuteActivationsAsync(Guid academicYearId, AdmissionCrmBulkExecuteActivationsDto input)
	{
		await EnsureBatchAsync(academicYearId, input.ActivationIds, nameof(ExecuteActivationsAsync));
		return await RunAsync(input.ActivationIds.Distinct().ToList(), x => x, async activationId =>
		{
			var activation = await _activationRepository.GetAsync(activationId);
			if (activation.AcademicYearId != academicYearId)
				throw new BusinessException("AdmissionCrm:BulkAcademicYearMismatch");
			var result = await _activationWorkflow.ExecuteAsync(activationId);
			return result.StudentId?.ToString() ?? result.Id.ToString();
		});
	}

	private async Task EnsureBatchAsync(Guid academicYearId, IEnumerable<Guid> ids, string operation)
	{
		await _scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, operation);
		var values = ids.Distinct().ToList();
		if (values.Count == 0 || values.Count > MaximumBatchSize || values.Any(x => x == Guid.Empty))
			throw new BusinessException("AdmissionCrm:InvalidBulkBatch").WithData("MaximumBatchSize", MaximumBatchSize);
	}

	private static async Task<AdmissionCrmBulkOperationResultDto> RunAsync<T>(
		IReadOnlyCollection<T> values, Func<T, Guid> id, Func<T, Task<string?>> operation)
	{
		var items = new List<AdmissionCrmBulkOperationItemResultDto>();
		foreach (var value in values)
		{
			try
			{
				items.Add(new AdmissionCrmBulkOperationItemResultDto
				{
					EntityId = id(value), Success = true, ResultId = await operation(value)
				});
			}
			catch (Exception exception)
			{
				items.Add(Failure(id(value), exception));
			}
		}
		return Summarize(items);
	}

	private static AdmissionCrmBulkOperationItemResultDto Failure(Guid id, Exception exception) => new()
	{
		EntityId = id,
		Success = false,
		ErrorCode = exception switch
		{
			BusinessException businessException => businessException.Code ?? "AdmissionCrm:BulkItemFailed",
			AbpAuthorizationException => "AdmissionCrm:BulkItemUnauthorized",
			_ => "AdmissionCrm:BulkItemFailed"
		},
		Message = exception is BusinessException ? exception.Message : "The item could not be processed."
	};

	private static AdmissionCrmBulkOperationResultDto Summarize(IReadOnlyList<AdmissionCrmBulkOperationItemResultDto> items) => new()
	{
		RequestedCount = items.Count,
		SucceededCount = items.Count(x => x.Success),
		FailedCount = items.Count(x => !x.Success),
		Items = items
	};

	private static bool IsProtectedBulkStage(AdmissionInquiryStage stage) =>
		stage is AdmissionInquiryStage.ExamReady
			or AdmissionInquiryStage.ExamScheduled
			or AdmissionInquiryStage.ExamCompleted
			or AdmissionInquiryStage.Eligible
			or AdmissionInquiryStage.Waitlisted
			or AdmissionInquiryStage.Rejected
			or AdmissionInquiryStage.Archived;
}
