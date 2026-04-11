using ShikshaMitra.Constants;
using ShikshaMitra.Students;
using System;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Library.Members;

public class LibraryMemberManager : DomainService
{
	public string GetMemberIdentifier(Student student)
	{
		var randomGenerator = new Random();
		return $"{AppConstants.LibraryCode}-{randomGenerator.Next(1000, 9999)}";
	}
}
