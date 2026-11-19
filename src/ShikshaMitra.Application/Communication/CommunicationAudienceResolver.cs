using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.Groups;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication;

public interface ICommunicationAudienceResolver
{
	Task<CommunicationAudienceResolutionResult> ResolveAsync(Guid campaignId, CommunicationAudienceDefinitionDto audience);
}

public class CommunicationAudienceResolutionResult
{
	public List<CommunicationRecipientSnapshot> Recipients { get; set; } = new();
	public List<string> Issues { get; set; } = new();
	public List<string> BlockingIssues { get; set; } = new();
	public int GroupRecipientCount { get; set; }
	public int ExplicitRecipientCount { get; set; }
	public int FilterMatchedCount { get; set; }
}

public class CommunicationAudienceResolver(
	IGroupAppService groupAppService,
	IRepository<IdentityUser, Guid> userRepository,
	IStudentRepository studentRepository,
	IEmployeeRepository employeeRepository,
	IGuidGenerator guidGenerator,
	ICurrentTenant currentTenant,
	IAsyncQueryableExecuter asyncExecuter)
	: ICommunicationAudienceResolver, ITransientDependency
{
	public async Task<CommunicationAudienceResolutionResult> ResolveAsync(Guid campaignId, CommunicationAudienceDefinitionDto audience)
	{
		var result = new CommunicationAudienceResolutionResult();
		var recipients = new Dictionary<string, CommunicationRecipientSnapshot>(StringComparer.OrdinalIgnoreCase);

		foreach (var explicitRecipient in NormalizeExplicitRecipients(audience.ExplicitRecipients))
		{
			AddRecipient(recipients, CreateRecipient(
				campaignId,
				explicitRecipient.RecipientType,
				explicitRecipient.RecipientId,
				explicitRecipient.UserId,
				explicitRecipient.DisplayName,
				explicitRecipient.Email,
				explicitRecipient.MobileNumber,
				explicitRecipient.PreferredLanguageCode,
				explicitRecipient.IsParentDefault));
		}

		result.ExplicitRecipientCount = recipients.Count;

		var userIds = audience.UserIds.Distinct().ToList();
		foreach (var groupId in audience.GroupIds.Distinct())
		{
			var groupMembers = await groupAppService.GetGroupMembersAsync(groupId);
			result.GroupRecipientCount += groupMembers.Count;
			userIds.AddRange(groupMembers);
		}

		await AddIdentityUsersAsync(campaignId, recipients, userIds.Distinct().ToList(), result);
		await AddEmployeesAsync(campaignId, recipients, audience.EmployeeIds.Distinct().ToList(), result);
		await AddStudentAudienceAsync(campaignId, recipients, audience, result);

		if (audience.ParentGuardianIds.Count > 0)
		{
			result.BlockingIssues.Add("Parent/guardian IDs are not a stable source entity yet; select students or explicit recipients for parent-first delivery.");
		}

		AddUnsupportedAudienceIssues(audience, result);

		result.Recipients = recipients.Values.ToList();
		return result;
	}

	private async Task AddIdentityUsersAsync(
		Guid campaignId,
		Dictionary<string, CommunicationRecipientSnapshot> recipients,
		List<Guid> userIds,
		CommunicationAudienceResolutionResult result)
	{
		if (userIds.Count == 0)
		{
			return;
		}

		var userQuery = await userRepository.GetQueryableAsync();
		var users = await asyncExecuter.ToListAsync(userQuery.Where(x => userIds.Contains(x.Id)));
		var foundIds = users.Select(x => x.Id).ToHashSet();

		foreach (var user in users)
		{
			AddRecipient(recipients, CreateRecipient(
				campaignId,
				"User",
				user.Id,
				user.Id,
				BuildUserDisplayName(user),
				user.Email,
				user.PhoneNumber,
				"en",
				false));
		}

		foreach (var missingUserId in userIds.Where(x => !foundIds.Contains(x)))
		{
			result.Issues.Add($"Identity user {missingUserId} was not found and was excluded from the audience.");
		}
	}

	private async Task AddEmployeesAsync(
		Guid campaignId,
		Dictionary<string, CommunicationRecipientSnapshot> recipients,
		List<Guid> employeeIds,
		CommunicationAudienceResolutionResult result)
	{
		if (employeeIds.Count == 0)
		{
			return;
		}

		var employeeQuery = await employeeRepository.GetQueryableAsync();
		var employees = await asyncExecuter.ToListAsync(employeeQuery.Where(x => employeeIds.Contains(x.Id)));
		var foundIds = employees.Select(x => x.Id).ToHashSet();

		foreach (var employee in employees)
		{
			AddRecipient(recipients, CreateRecipient(
				campaignId,
				"Employee",
				employee.Id,
				employee.UserId,
				employee.EmployeeName.IsNullOrWhiteSpace() ? employee.EmployeeCode : employee.EmployeeName,
				employee.Email,
				employee.MobileNo,
				"en",
				false));
		}

		foreach (var missingEmployeeId in employeeIds.Where(x => !foundIds.Contains(x)))
		{
			result.Issues.Add($"Employee {missingEmployeeId} was not found and was excluded from the audience.");
		}
	}

	private async Task AddStudentAudienceAsync(
		Guid campaignId,
		Dictionary<string, CommunicationRecipientSnapshot> recipients,
		CommunicationAudienceDefinitionDto audience,
		CommunicationAudienceResolutionResult result)
	{
		var studentIds = audience.StudentIds.Distinct().ToList();
		if (audience.ClassIds.Count > 0 || audience.DivisionIds.Count > 0)
		{
			var studentQuery = await studentRepository.GetQueryableWithoutAutoIncludesAsync();

			if (audience.ClassIds.Count > 0)
			{
				var classIds = audience.ClassIds.Distinct().ToList();
				studentQuery = studentQuery.Where(x => x.StandardId.HasValue && classIds.Contains(x.StandardId.Value));
			}

			if (audience.DivisionIds.Count > 0)
			{
				var divisionIds = audience.DivisionIds.Distinct().ToList();
				studentQuery = studentQuery.Where(x => x.DivisionId.HasValue && divisionIds.Contains(x.DivisionId.Value));
			}

			var matchedStudentIds = await asyncExecuter.ToListAsync(studentQuery.Select(x => x.Id));
			result.FilterMatchedCount += matchedStudentIds.Count;
			studentIds.AddRange(matchedStudentIds);
		}

		if (studentIds.Count == 0)
		{
			return;
		}

		var distinctStudentIds = studentIds.Distinct().ToList();
		var directStudentQuery = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
		var students = await asyncExecuter.ToListAsync(
			directStudentQuery
				.Where(x => distinctStudentIds.Contains(x.Id))
				.Select(x => new StudentAudienceProjection
				{
					Id = x.Id,
					ExtraProperties = x.ExtraProperties,
					FirstName = x.FirstName,
					MiddleName = x.MiddleName,
					LastName = x.LastName,
					ContactEmail = x.ContactDetail == null ? null : x.ContactDetail.Email,
					ContactMobileNumber = x.ContactDetail == null ? null : x.ContactDetail.PrimaryContactNo,
					FatherFullName = x.FamilyDetail == null || x.FamilyDetail.FatherDetails == null ? null : x.FamilyDetail.FatherDetails.FullName,
					FatherEmail = x.FamilyDetail == null || x.FamilyDetail.FatherDetails == null ? null : x.FamilyDetail.FatherDetails.Email,
					FatherContactNo = x.FamilyDetail == null || x.FamilyDetail.FatherDetails == null ? null : x.FamilyDetail.FatherDetails.ContactNo,
					MotherFullName = x.FamilyDetail == null || x.FamilyDetail.MotherDetails == null ? null : x.FamilyDetail.MotherDetails.FullName,
					MotherEmail = x.FamilyDetail == null || x.FamilyDetail.MotherDetails == null ? null : x.FamilyDetail.MotherDetails.Email,
					MotherContactNo = x.FamilyDetail == null || x.FamilyDetail.MotherDetails == null ? null : x.FamilyDetail.MotherDetails.ContactNo,
					GuardianFullName = x.FamilyDetail == null || x.FamilyDetail.GuardianDetails == null ? null : x.FamilyDetail.GuardianDetails.FullName,
					GuardianEmail = x.FamilyDetail == null || x.FamilyDetail.GuardianDetails == null ? null : x.FamilyDetail.GuardianDetails.Email,
					GuardianContactNo = x.FamilyDetail == null || x.FamilyDetail.GuardianDetails == null ? null : x.FamilyDetail.GuardianDetails.ContactNo
				}));
		var foundIds = students.Select(x => x.Id).ToHashSet();

		foreach (var student in students)
		{
			AddStudentRecipients(campaignId, recipients, student, audience);
		}

		foreach (var missingStudentId in distinctStudentIds.Where(x => !foundIds.Contains(x)))
		{
			result.Issues.Add($"Student {missingStudentId} was not found and was excluded from the audience.");
		}
	}

	private void AddStudentRecipients(
		Guid campaignId,
		Dictionary<string, CommunicationRecipientSnapshot> recipients,
		StudentAudienceProjection student,
		CommunicationAudienceDefinitionDto audience)
	{
		var studentName = BuildStudentDisplayName(student);
		var parentRecipients = new List<CommunicationRecipientSnapshot>();
		var selected = student.ExtraProperties.GetValueOrDefault(StudentFamilyContactSelection.SelectionKey)?.ToString();

		if (audience.ParentsFirst)
		{
			var available = new List<string>();
			if (!student.FatherFullName.IsNullOrWhiteSpace() || !student.FatherEmail.IsNullOrWhiteSpace() || !student.FatherContactNo.IsNullOrWhiteSpace()) available.Add("father");
			if (!student.MotherFullName.IsNullOrWhiteSpace() || !student.MotherEmail.IsNullOrWhiteSpace() || !student.MotherContactNo.IsNullOrWhiteSpace()) available.Add("mother");
			if (!student.GuardianFullName.IsNullOrWhiteSpace() || !student.GuardianEmail.IsNullOrWhiteSpace() || !student.GuardianContactNo.IsNullOrWhiteSpace()) available.Add("guardian");
			foreach (var relationship in StudentFamilyContactSelection.MessageRelationships(available, selected))
			{
				if (relationship == "father") AddParentRecipient(parentRecipients, campaignId, student, "Father", student.FatherFullName, student.FatherEmail, student.FatherContactNo, studentName);
				else if (relationship == "mother") AddParentRecipient(parentRecipients, campaignId, student, "Mother", student.MotherFullName, student.MotherEmail, student.MotherContactNo, studentName);
				else
				{
					var label = student.ExtraProperties.GetValueOrDefault(StudentFamilyContactSelection.GuardianRelationshipKey)?.ToString();
					AddParentRecipient(parentRecipients, campaignId, student, string.IsNullOrWhiteSpace(label) ? "Guardian" : $"Guardian ({label})", student.GuardianFullName, student.GuardianEmail, student.GuardianContactNo, studentName);
				}
			}
		}

		if (parentRecipients.Count == 0 && (!audience.ParentsFirst || selected.IsNullOrWhiteSpace()) && (!student.ContactEmail.IsNullOrWhiteSpace() || !student.ContactMobileNumber.IsNullOrWhiteSpace()))
		{
			parentRecipients.Add(CreateRecipient(
				campaignId,
				"ParentGuardian",
				student.Id,
				null,
				$"Parent/Guardian of {studentName}",
				student.ContactEmail,
				student.ContactMobileNumber,
				"en",
				true));
		}

		foreach (var parentRecipient in parentRecipients)
		{
			AddRecipient(recipients, parentRecipient);
		}

		if (audience.IncludeStudentsWhenEligible && (!student.ContactEmail.IsNullOrWhiteSpace() || !student.ContactMobileNumber.IsNullOrWhiteSpace()))
		{
			AddRecipient(recipients, CreateRecipient(
				campaignId,
				"Student",
				student.Id,
				null,
				studentName,
				student.ContactEmail,
				student.ContactMobileNumber,
				"en",
				false));
		}
	}

	private void AddParentRecipient(
		List<CommunicationRecipientSnapshot> recipients,
		Guid campaignId,
		StudentAudienceProjection student,
		string relationship,
		string? fullName,
		string? email,
		string? contactNo,
		string studentName)
	{
		if (fullName.IsNullOrWhiteSpace() && email.IsNullOrWhiteSpace() && contactNo.IsNullOrWhiteSpace())
		{
			return;
		}

		recipients.Add(CreateRecipient(
			campaignId,
			"ParentGuardian",
			student.Id,
			null,
			fullName.IsNullOrWhiteSpace() ? $"{relationship} of {studentName}" : $"{fullName} ({relationship} of {studentName})",
			email,
			contactNo,
			"en",
			true));
	}

	private static void AddUnsupportedAudienceIssues(
		CommunicationAudienceDefinitionDto audience,
		CommunicationAudienceResolutionResult result)
	{
		if (audience.OrganizationIds.Count > 0)
		{
			result.BlockingIssues.Add("Organization audience filters need the organization/user-scope adapter before publish.");
		}

		if (audience.Roles.Count > 0)
		{
			result.BlockingIssues.Add("Role audience filters need the role adapter before publish.");
		}

		foreach (var filter in audience.Filters)
		{
			if (filter.Field.StartsWith("finance", StringComparison.OrdinalIgnoreCase)
				|| filter.Field.StartsWith("fee", StringComparison.OrdinalIgnoreCase)
				|| filter.Field.StartsWith("transport", StringComparison.OrdinalIgnoreCase)
				|| filter.Field.StartsWith("route", StringComparison.OrdinalIgnoreCase)
				|| filter.Field.StartsWith("hostel", StringComparison.OrdinalIgnoreCase))
			{
				result.BlockingIssues.Add($"{filter.Field} audience filter needs its source-module adapter before publish.");
			}
			else
			{
				result.BlockingIssues.Add($"{filter.Field} audience filter is not supported by the V1 resolver.");
			}
		}
	}

	private CommunicationRecipientSnapshot CreateRecipient(
		Guid campaignId,
		string recipientType,
		Guid? recipientId,
		Guid? userId,
		string displayName,
		string? email,
		string? mobileNumber,
		string preferredLanguageCode,
		bool isParentDefault)
	{
		var normalizedEmail = email?.Trim();
		var normalizedMobile = mobileNumber?.Trim();
		var hasDestination = !normalizedEmail.IsNullOrWhiteSpace() || !normalizedMobile.IsNullOrWhiteSpace() || userId.HasValue;

		return new CommunicationRecipientSnapshot(guidGenerator.Create(), currentTenant.Id)
		{
			CampaignId = campaignId,
			RecipientType = recipientType,
			RecipientId = recipientId,
			UserId = userId,
			DisplayName = displayName.IsNullOrWhiteSpace() ? "Recipient" : displayName.Trim(),
			Email = normalizedEmail,
			MobileNumber = normalizedMobile,
			PreferredLanguageCode = preferredLanguageCode.IsNullOrWhiteSpace() ? "en" : preferredLanguageCode.Trim(),
			IsParentDefault = isParentDefault,
			ChannelEligibilityJson = BuildChannelEligibilityJson(normalizedEmail, normalizedMobile, userId),
			IsEligible = hasDestination,
			BlockedReason = hasDestination ? null : "No channel destination is available."
		};
	}

	private static void AddRecipient(Dictionary<string, CommunicationRecipientSnapshot> recipients, CommunicationRecipientSnapshot recipient)
	{
		var key = recipient.UserId.HasValue
			? $"user:{recipient.UserId}"
			: $"{recipient.RecipientType}:{recipient.RecipientId}:{recipient.Email}:{recipient.MobileNumber}:{recipient.DisplayName}";

		recipients.TryAdd(key, recipient);
	}

	private static string BuildUserDisplayName(IdentityUser user)
	{
		var name = string.Join(" ", new[] { user.Name, user.Surname }.Where(x => !x.IsNullOrWhiteSpace()));
		return name.IsNullOrWhiteSpace() ? user.UserName : name;
	}

	private static string BuildStudentDisplayName(StudentAudienceProjection student)
	{
		var name = string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }.Where(x => !x.IsNullOrWhiteSpace()));
		return name.IsNullOrWhiteSpace() ? $"Student {student.Id}" : name;
	}

	private sealed class StudentAudienceProjection
	{
		public ExtraPropertyDictionary ExtraProperties { get; init; } = new();
		public Guid Id { get; init; }
		public string? FirstName { get; init; }
		public string? MiddleName { get; init; }
		public string? LastName { get; init; }
		public string? ContactEmail { get; init; }
		public string? ContactMobileNumber { get; init; }
		public string? FatherFullName { get; init; }
		public string? FatherEmail { get; init; }
		public string? FatherContactNo { get; init; }
		public string? MotherFullName { get; init; }
		public string? MotherEmail { get; init; }
		public string? MotherContactNo { get; init; }
		public string? GuardianFullName { get; init; }
		public string? GuardianEmail { get; init; }
		public string? GuardianContactNo { get; init; }
	}

	private static List<CommunicationExplicitRecipientDto> NormalizeExplicitRecipients(List<CommunicationExplicitRecipientDto> recipients)
	{
		return recipients
			.Where(x => !x.DisplayName.IsNullOrWhiteSpace() || x.RecipientId.HasValue || x.UserId.HasValue || !x.Email.IsNullOrWhiteSpace() || !x.MobileNumber.IsNullOrWhiteSpace())
			.Select(x => new CommunicationExplicitRecipientDto
			{
				RecipientType = x.RecipientType.IsNullOrWhiteSpace() ? "ParentGuardian" : x.RecipientType.Trim(),
				RecipientId = x.RecipientId,
				UserId = x.UserId,
				DisplayName = x.DisplayName.IsNullOrWhiteSpace() ? x.Email ?? x.MobileNumber ?? "Recipient" : x.DisplayName.Trim(),
				Email = x.Email?.Trim(),
				MobileNumber = x.MobileNumber?.Trim(),
				PreferredLanguageCode = x.PreferredLanguageCode.IsNullOrWhiteSpace() ? "en" : x.PreferredLanguageCode.Trim(),
				IsParentDefault = x.IsParentDefault
			})
			.ToList();
	}

	private static string BuildChannelEligibilityJson(string? email, string? mobileNumber, Guid? userId)
	{
		return System.Text.Json.JsonSerializer.Serialize(new Dictionary<CommunicationChannelType, bool>
		{
			[CommunicationChannelType.WhatsApp] = !mobileNumber.IsNullOrWhiteSpace(),
			[CommunicationChannelType.Sms] = !mobileNumber.IsNullOrWhiteSpace(),
			[CommunicationChannelType.Email] = !email.IsNullOrWhiteSpace(),
			[CommunicationChannelType.InApp] = userId.HasValue
		});
	}
}
