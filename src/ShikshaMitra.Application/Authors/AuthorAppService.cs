using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Security.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Authors;

[Authorize(ShikshaMitraPermissions.Authors.Default)]
public class AuthorAppService(
	IAuthorRepository authorRepository,
	AuthorManager authorManager)
	: ShikshaMitraAppService, IAuthorAppService
{
	public async Task<AuthorDto> GetAsync(Guid id)
    {
        var author = await authorRepository.GetAsync(id);
        return ObjectMapper.Map<Author, AuthorDto>(author);
    }

    public async Task<PagedResultDto<AuthorDto>> GetListAsync(GetAuthorListDto input)
    {
        if (input.Sorting.IsNullOrWhiteSpace())
        {
            input.Sorting = nameof(Author.Name);
        }

        var authors = await authorRepository.GetListAsync(
            input.SkipCount,
            input.MaxResultCount,
            input.Sorting,
            input.Filter
        );

        var totalCount = input.Filter == null
            ? await authorRepository.CountAsync()
            : await authorRepository.CountAsync(
                author => author.Name.Contains(input.Filter));

        return new PagedResultDto<AuthorDto>(
            totalCount,
            ObjectMapper.Map<List<Author>, List<AuthorDto>>(authors)
        );
    }

    [Authorize(ShikshaMitraPermissions.Authors.Create)]
    public async Task<AuthorDto> CreateAsync(CreateAuthorDto input)
    {
        var author = await authorManager.CreateAsync(
            input.Name,
            input.BirthDate
        );

        await authorRepository.InsertAsync(author);

        return ObjectMapper.Map<Author, AuthorDto>(author);
    }

    [Authorize(ShikshaMitraPermissions.Authors.Edit)]
    public async Task UpdateAsync(Guid id, UpdateAuthorDto input)
    {
        var author = await authorRepository.GetAsync(id);

        if (author.Name != input.Name)
        {
            await authorManager.ChangeNameAsync(author, input.Name);
        }

        author.BirthDate = input.BirthDate;

        await authorRepository.UpdateAsync(author);
    }

    [Authorize(ShikshaMitraPermissions.Authors.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await authorRepository.DeleteAsync(id);
    }
}