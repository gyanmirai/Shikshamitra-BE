using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{



	private async Task<AdmissionWorkflowDto> CreateFeeSelectionWorkflowAsync(Guid registrationFormId, AdmissionPaymentCompletionPolicy? completion = null)
	{
		var startId = Guid.NewGuid();
		var paymentId = Guid.NewGuid();
		var doneId = Guid.NewGuid();
		var workflow = await _workflowAppService.CreateAsync(new()
		{
			Name = "Managed payment", Code = $"PAY_{Guid.NewGuid():N}"[..24],
			Stages =
			[
				new() { Id = startId, Code = "START", Name = "Ready for payment",
					StageType = AdmissionWorkflowStageType.Form, DisplayOrder = 0,
					IsEntryStage = true, IsRequired = true,
					ConfigurationJson = JsonSerializer.Serialize(new { formDefinitionId = registrationFormId }) },
				new() { Id = paymentId, Code = "PAYMENT", Name = "Application payment",
					StageType = AdmissionWorkflowStageType.Payment, DisplayOrder = 1, IsRequired = true,
					ConfigurationJson = completion == null ? null : JsonSerializer.Serialize(new { paymentCompletion = completion }) },
				new() { Id = doneId, Code = "DONE", Name = "Payment complete",
					StageType = AdmissionWorkflowStageType.Approval, DisplayOrder = 2,
					IsTerminalStage = true, IsRequired = true }
			],
			Transitions =
			[
				new() { FromStageId = startId, ToStageId = paymentId, Priority = 1, IsDefault = true },
				new() { FromStageId = paymentId, ToStageId = doneId, Priority = 1, IsDefault = true }
			]
		});
		return await _workflowAppService.PublishAsync(workflow.Id, workflow.ConcurrencyStamp);
	}

	private static ClaimsPrincipal BuildPaymentPrincipal(Guid userId) =>
		new(new ClaimsIdentity(
		[
			new Claim(AbpClaimTypes.UserId, userId.ToString()),
			new Claim(AbpClaimTypes.UserName, $"payment-admin-{userId:N}"),
			new Claim(AbpClaimTypes.Role, "admin")
		], "Test"));
}
