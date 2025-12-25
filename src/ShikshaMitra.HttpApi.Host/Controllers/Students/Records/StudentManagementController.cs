using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.Records;


public class StudentManagementController : AbpController
{
	public IActionResult Index()
	{
		return View();
	}
}
