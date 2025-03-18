using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Headers;
using ShikshaMitra.Models;

namespace ShikshaMitra.Extensions;

public static class ControllerExtensions
{
	/// <summary>
	/// Get the HeaderValues from the current controller context
	/// </summary>
	public static HeaderValues Headers(this ControllerBase controller)
	{
		return controller.HttpContext.RequestServices
			.GetService(typeof(IHeaderValuesService)) as IHeaderValuesService
			is IHeaderValuesService service ? service.Current : new HeaderValues();
	}

	/// <summary>
	/// Validates that all required header GUIDs are present
	/// Returns true if valid, otherwise sets result and returns false
	/// </summary>
	public static bool ValidateRequiredHeaders(this ControllerBase controller, out IActionResult result)
	{
		var headers = controller.Headers();

		if (!headers.HasValidAcademicYear)
		{
			result = controller.BadRequest("AcademicYearId is required");
			return false;
		}

		if (!headers.HasValidEducationBoard)
		{
			result = controller.BadRequest("EducationBoardId is required");
			return false;
		}

		if (!headers.HasValidEducationMedium)
		{
			result = controller.BadRequest("EducationMediumId is required");
			return false;
		}

		result = null;
		return true;
	}
}
