using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionWorkflowStageRuntimeContext(
	Guid ApplicationId,
	Guid WorkflowInstanceId,
	Guid StageInstanceId,
	AdmissionApplicationStatus ApplicationStatus);

public interface IAdmissionWorkflowStageHandler : ITransientDependency
{
	AdmissionWorkflowStageType StageType { get; }
	Task OnEnterAsync(AdmissionWorkflowStageRuntimeContext context);
	Task<bool> CanCompleteAsync(AdmissionWorkflowStageRuntimeContext context);
	Task OnCompleteAsync(AdmissionWorkflowStageRuntimeContext context);
}

public class InquiryAdmissionWorkflowStageHandler : IAdmissionWorkflowStageHandler
{
	public AdmissionWorkflowStageType StageType => AdmissionWorkflowStageType.Inquiry;
	public Task OnEnterAsync(AdmissionWorkflowStageRuntimeContext context) => Task.CompletedTask;
	public Task<bool> CanCompleteAsync(AdmissionWorkflowStageRuntimeContext context) => Task.FromResult(true);
	public Task OnCompleteAsync(AdmissionWorkflowStageRuntimeContext context) => Task.CompletedTask;
}

public class FormAdmissionWorkflowStageHandler : IAdmissionWorkflowStageHandler
{
	public AdmissionWorkflowStageType StageType => AdmissionWorkflowStageType.Form;
	public Task OnEnterAsync(AdmissionWorkflowStageRuntimeContext context) => Task.CompletedTask;
	public Task<bool> CanCompleteAsync(AdmissionWorkflowStageRuntimeContext context) =>
		Task.FromResult(context.ApplicationStatus == AdmissionApplicationStatus.Submitted);
	public Task OnCompleteAsync(AdmissionWorkflowStageRuntimeContext context) => Task.CompletedTask;
}

public class CustomAdmissionWorkflowStageHandler : IAdmissionWorkflowStageHandler
{
	public AdmissionWorkflowStageType StageType => AdmissionWorkflowStageType.Custom;
	public Task OnEnterAsync(AdmissionWorkflowStageRuntimeContext context) => Task.CompletedTask;
	public Task<bool> CanCompleteAsync(AdmissionWorkflowStageRuntimeContext context) => Task.FromResult(true);
	public Task OnCompleteAsync(AdmissionWorkflowStageRuntimeContext context) => Task.CompletedTask;
}

public abstract class SubmittedApplicationAdmissionWorkflowStageHandler : IAdmissionWorkflowStageHandler
{
	public abstract AdmissionWorkflowStageType StageType { get; }
	public Task OnEnterAsync(AdmissionWorkflowStageRuntimeContext context) => Task.CompletedTask;
	public Task<bool> CanCompleteAsync(AdmissionWorkflowStageRuntimeContext context) =>
		Task.FromResult(context.ApplicationStatus == AdmissionApplicationStatus.Submitted);
	public Task OnCompleteAsync(AdmissionWorkflowStageRuntimeContext context) => Task.CompletedTask;
}

public sealed class DocumentVerificationAdmissionWorkflowStageHandler : SubmittedApplicationAdmissionWorkflowStageHandler
{
	public override AdmissionWorkflowStageType StageType => AdmissionWorkflowStageType.DocumentVerification;
}

public sealed class ApprovalAdmissionWorkflowStageHandler : SubmittedApplicationAdmissionWorkflowStageHandler
{
	public override AdmissionWorkflowStageType StageType => AdmissionWorkflowStageType.Approval;
}

public sealed class EnrollmentAdmissionWorkflowStageHandler : SubmittedApplicationAdmissionWorkflowStageHandler
{
	public override AdmissionWorkflowStageType StageType => AdmissionWorkflowStageType.Enrollment;
}

public class AdmissionWorkflowStageHandlerRegistry(
	IEnumerable<IAdmissionWorkflowStageHandler> handlers) : DomainService
{
	private readonly IReadOnlyDictionary<AdmissionWorkflowStageType, IAdmissionWorkflowStageHandler> _handlers =
		handlers.ToDictionary(handler => handler.StageType);

	public async Task EnsureCanCompleteAsync(
		AdmissionWorkflowStageType stageType,
		AdmissionWorkflowStageRuntimeContext context)
	{
		if (!_handlers.TryGetValue(stageType, out var handler) || !await handler.CanCompleteAsync(context))
		{
			throw new BusinessException(
				AdmissionApplicationErrorCodes.StageCannotAdvance,
				$"The {stageType} stage cannot be completed in the current application state.");
		}
	}

	public Task OnEnterAsync(AdmissionWorkflowStageType stageType, AdmissionWorkflowStageRuntimeContext context) =>
		_handlers.TryGetValue(stageType, out var handler) ? handler.OnEnterAsync(context) : Task.CompletedTask;

	public Task OnCompleteAsync(AdmissionWorkflowStageType stageType, AdmissionWorkflowStageRuntimeContext context) =>
		_handlers.TryGetValue(stageType, out var handler) ? handler.OnCompleteAsync(context) : Task.CompletedTask;
}
