using System;
using System.Linq;
using ShikshaMitra.Students;
using Volo.Abp.Data;

namespace ShikshaMitra.Library.Notifications;

public sealed record LibraryNotificationContactFacts(string? FullName, string? Email, string? ContactNo)
{
	public bool IsPresent => !string.IsNullOrWhiteSpace(FullName) || !string.IsNullOrWhiteSpace(Email) || !string.IsNullOrWhiteSpace(ContactNo);
}

public sealed record LibraryNotificationStudentFacts(Guid Id, Guid? UserId, string? FirstName, string? MiddleName,
	string? LastName, StudentLifeCycleStatusEnum Status, bool IsLibraryMember, string? Email, string? Phone,
	ExtraPropertyDictionary ExtraProperties, LibraryNotificationContactFacts? Father,
	LibraryNotificationContactFacts? Mother, LibraryNotificationContactFacts? Guardian) : IHasExtraProperties
{
	public string Name => string.Join(" ", new[] { FirstName, MiddleName, LastName }.Where(part => !string.IsNullOrWhiteSpace(part)));
	public LibraryNotificationContactFacts? Profile(string relationship) => relationship switch
	{
		"father" => Father, "mother" => Mother, "guardian" => Guardian, _ => null
	};
}

public sealed record LibraryNotificationEmployeeFacts(Guid Id, Guid? UserId, string? FirstName, string? LastName,
	string? Email, string? Phone, bool IsLibraryMember);
