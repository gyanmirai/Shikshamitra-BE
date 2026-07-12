using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public sealed record AdmissionCrmIntegrationResult(
	AdmissionCrmIntegrationCapabilityState State,
	bool Succeeded,
	string Detail,
	string? ExternalReference = null);

public interface IAdmissionCrmFinanceStatusPort
{
	Task<AdmissionCrmIntegrationResult> SynchronizeAsync(Guid leadId);
}

public interface IAdmissionCrmGovernmentExchangePort
{
	Task<AdmissionCrmIntegrationResult> ExchangeAsync(Guid academicYearId, string operation);
}

public interface IAdmissionCrmOptionalServiceHandoffPort
{
	Task<AdmissionCrmIntegrationResult> DispatchAsync(Guid activationId, string serviceCode);
}

public interface IAdmissionCrmDocumentSigningPort
{
	Task<AdmissionCrmIntegrationResult> SignAsync(Guid generatedDocumentId);
}

public class ManualAdmissionCrmFinanceStatusPort : IAdmissionCrmFinanceStatusPort, ITransientDependency
{
	public Task<AdmissionCrmIntegrationResult> SynchronizeAsync(Guid leadId) =>
		Task.FromResult(new AdmissionCrmIntegrationResult(
			AdmissionCrmIntegrationCapabilityState.Manual,
			false,
			"Finance synchronization is manual; record the verified payment reference in Enrollment."));
}

public class NotConfiguredAdmissionCrmGovernmentExchangePort : IAdmissionCrmGovernmentExchangePort, ITransientDependency
{
	public Task<AdmissionCrmIntegrationResult> ExchangeAsync(Guid academicYearId, string operation) =>
		Task.FromResult(new AdmissionCrmIntegrationResult(
			AdmissionCrmIntegrationCapabilityState.NotConfigured,
			false,
			"No government exchange adapter is configured; validated file import and export remain available."));
}

public class ManualAdmissionCrmOptionalServiceHandoffPort : IAdmissionCrmOptionalServiceHandoffPort, ITransientDependency
{
	public Task<AdmissionCrmIntegrationResult> DispatchAsync(Guid activationId, string serviceCode) =>
		Task.FromResult(new AdmissionCrmIntegrationResult(
			AdmissionCrmIntegrationCapabilityState.Manual,
			false,
			"The service interest is recorded for manual handoff; fulfillment is owned by the destination module."));
}

public class NotConfiguredAdmissionCrmDocumentSigningPort : IAdmissionCrmDocumentSigningPort, ITransientDependency
{
	public Task<AdmissionCrmIntegrationResult> SignAsync(Guid generatedDocumentId) =>
		Task.FromResult(new AdmissionCrmIntegrationResult(
			AdmissionCrmIntegrationCapabilityState.NotConfigured,
			false,
			"Generated documents are unsigned because no signing adapter is configured."));
}
