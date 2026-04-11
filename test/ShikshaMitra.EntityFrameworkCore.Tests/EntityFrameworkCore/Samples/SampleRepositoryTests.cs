using Microsoft.EntityFrameworkCore;
using Shouldly;
using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Samples;

[Collection(ShikshaMitraTestConsts.CollectionDefinitionName)]
public class SampleRepositoryTests : ShikshaMitraEntityFrameworkCoreTestBase
{
	private readonly IRepository<IdentityUser, Guid> _appUserRepository;
	private readonly IGuidGenerator _guidGenerator;

	public SampleRepositoryTests()
	{
		_appUserRepository = GetRequiredService<IRepository<IdentityUser, Guid>>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
	}

	[Fact]
	public async Task Should_Query_AppUser()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			var user = new IdentityUser(_guidGenerator.Create(), "testuser", "testuser@test.com");
			await _appUserRepository.InsertAsync(user, autoSave: true);

			var anyUser = await (await _appUserRepository.GetQueryableAsync())
				.FirstOrDefaultAsync();

			anyUser.ShouldNotBeNull();
		});
	}
}
