using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPublicCommunicationPreferences(
	IRepository<AdmissionCrmCommunicationOptOut, Guid> optOutRepository) : ApplicationService, ITransientDependency
{
	public async Task<PublicAdmissionCommunicationPreferencesDto> GetAsync(AdmissionInquiryLead lead)
	{
		var hasPhone = !string.IsNullOrWhiteSpace(lead.PrimaryContactNo);
		var hasEmail = !string.IsNullOrWhiteSpace(lead.Email);
		return new PublicAdmissionCommunicationPreferencesDto
		{
			WhatsApp = hasPhone && !await HasActiveOptOutAsync(
				lead, AdmissionCrmMessageChannel.WhatsApp, lead.PrimaryContactNo!),
			Sms = hasPhone && !await HasActiveOptOutAsync(
				lead, AdmissionCrmMessageChannel.Sms, lead.PrimaryContactNo!),
			Email = hasEmail && !await HasActiveOptOutAsync(
				lead, AdmissionCrmMessageChannel.Email, lead.Email!),
			HasPhoneContact = hasPhone,
			HasEmailContact = hasEmail
		};
	}

	public async Task SetAsync(
		AdmissionInquiryLead lead,
		AdmissionCrmMessageChannel channel,
		string? contactValue,
		bool enabled,
		DateTime at)
	{
		if (string.IsNullOrWhiteSpace(contactValue))
		{
			return;
		}

		var normalizedContact = AdmissionCrmCommunicationOptOut.NormalizeContact(channel, contactValue);
		var queryable = await optOutRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.FirstOrDefaultAsync(queryable
			.Where(optOut => optOut.TenantId == CurrentTenant.Id &&
				optOut.AcademicYearId == lead.AcademicYearId && optOut.Channel == channel &&
				optOut.NormalizedContactValue == normalizedContact)
			.OrderByDescending(optOut => optOut.CreationTime));
		if (enabled)
		{
			if (existing is { IsActive: true })
			{
				existing.Clear("Family opted in through the secure applicant portal.", at);
				await optOutRepository.UpdateAsync(existing, autoSave: true);
			}

			return;
		}

		if (existing == null)
		{
			await optOutRepository.InsertAsync(
				new AdmissionCrmCommunicationOptOut(
					GuidGenerator.Create(), CurrentTenant.Id, lead.AcademicYearId, lead.Id, channel,
					contactValue, "Family opted out through the secure applicant portal.", at),
				autoSave: true);
		}
		else if (!existing.IsActive)
		{
			existing.Reactivate(lead.Id, "Family opted out through the secure applicant portal.", at);
			await optOutRepository.UpdateAsync(existing, autoSave: true);
		}
	}

	private async Task<bool> HasActiveOptOutAsync(
		AdmissionInquiryLead lead,
		AdmissionCrmMessageChannel channel,
		string contactValue)
	{
		var normalizedContact = AdmissionCrmCommunicationOptOut.NormalizeContact(channel, contactValue);
		var queryable = await optOutRepository.GetQueryableAsync();
		return await AsyncExecuter.AnyAsync(queryable.Where(optOut =>
			optOut.TenantId == CurrentTenant.Id && optOut.AcademicYearId == lead.AcademicYearId &&
			optOut.Channel == channel && optOut.NormalizedContactValue == normalizedContact && optOut.IsActive));
	}
}
