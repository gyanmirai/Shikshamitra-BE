using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Archive_reports_active_offering_count_and_preserves_workflow_until_dependencies_are_inactive()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var draft = await CreateDirectPublicationDraftAsync();
		var published = await _workflowAppService.PublishAsync(draft.Id, draft.ConcurrencyStamp);
		var first = await CreateAvailableOfferingAsync(published.Id, assertValid: false);
		var second = await CreateAvailableOfferingAsync(published.Id, assertValid: false);
		var inactive = await CreateAvailableOfferingAsync(published.Id, assertValid: false);
		await WithUnitOfWorkAsync(async () => {
			var offering = await _offeringRepository.GetAsync(inactive.Id);
			offering.Deactivate();
			await _offeringRepository.UpdateAsync(offering, autoSave: true);
		});
		var error = await Should.ThrowAsync<BusinessException>(() => _workflowAppService.ArchiveAsync(published.Id, published.ConcurrencyStamp));
		error.Code.ShouldBe(AdmissionWorkflowErrorCodes.WorkflowInUse);
		error.Data["ActiveOfferingCount"].ShouldBe(2);
		error.Data["Reason"].ShouldBeOfType<string>().ShouldContain("2 active admission offerings");
		var unchanged = await _workflowAppService.GetAsync(published.Id);
		unchanged.Status.ShouldBe(AdmissionWorkflowStatus.Published);
		unchanged.ConcurrencyStamp.ShouldBe(published.ConcurrencyStamp);
		await WithUnitOfWorkAsync(async () => {
			foreach (var id in new[] { first.Id, second.Id }) {
				var offering = await _offeringRepository.GetAsync(id);
				offering.Deactivate();
				await _offeringRepository.UpdateAsync(offering, autoSave: true);
			}
		});
		(await _workflowAppService.ArchiveAsync(published.Id, published.ConcurrencyStamp)).Status.ShouldBe(AdmissionWorkflowStatus.Archived);
	}
}
