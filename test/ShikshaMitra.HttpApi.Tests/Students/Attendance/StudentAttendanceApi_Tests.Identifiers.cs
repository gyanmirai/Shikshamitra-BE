using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.Attendance;

public partial class StudentAttendanceApi_Tests
{
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Roster_preserves_prefixed_and_legacy_gr_numbers(bool prefixed)
	{
		var context = await ResolveSeededDivisionContextAsync();
		var student = await CreateEnrolledStudentAsync(context, "GR contract");
		using (var services = Factory.Services.CreateScope())
		using (services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var repository = services.ServiceProvider.GetRequiredService<IStudentRepository>();
			var record = await repository.GetInquiryForUpdateAsync(student.StudentId)
				?? throw new InvalidOperationException("The identifier fixture student was not persisted.");
			if (prefixed) record.AssignGrNumber("QAS26-000001");
			else record.AssignGrNo(261012);
			await repository.UpdateAsync(record);
			await uow.CompleteAsync();
		}
		await EnsureWorkingDateAsync(context, context.ReferenceDate);
		var roster = await ReadRosterAsync(context, context.DivisionId, context.ReferenceDate);
		var row = roster.Students.Single(item => item.StudentId == student.StudentId);
		row.GrNumber.ShouldBe(prefixed ? "QAS26-000001" : "261012");
		row.GrNo.ShouldBe(prefixed ? null : 261012);
	}
}
