using ShikshaMitra.Models;
using System;

namespace ShikshaMitra.Common.Headers;

public interface IHeaderValuesService
{
	HeaderValues Current { get; }
	Guid GetAcademicYearId();
	Guid GetEducationBoardId();
	Guid GetEducationMediumId();
	string GetCurrentUser();
	DateTime GetRequestTime();
}