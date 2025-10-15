using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.Syllabus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
namespace ShikshaMitra.Academic.Curriculum;

[RemoteService(IsEnabled = false)]
public class ChapterrAppService(
    IRepository<Chapter, Guid> chapterRepository,
    IRepository<ChapterDocument, Guid> chapterDocumentRepository,
    IGuidGenerator guidGenerator
) : ApplicationService, IChapterrAppService
{
    public async Task<ChapterrDto> CreateChapterAsync(CreateChapterDto createChapterDto)
    {
        try
        {
            if (createChapterDto.StartDate > createChapterDto.EndDate)
                throw new UserFriendlyException("Start Date cannot be after End Date.");

            var existingChapterByName = await chapterRepository.FirstOrDefaultAsync(
                x => x.Name.ToLower() == createChapterDto.Name.ToLower()
                     && x.StandardId == createChapterDto.StandardId
                     && x.SubjectId == createChapterDto.SubjectId
                     && !x.IsDeleted
            );

            if (existingChapterByName != null)
                throw new UserFriendlyException(
                    $"A chapter named '{createChapterDto.Name}' already exists in this subject and class."
                );

            var existingChapterByNumber = await chapterRepository.FirstOrDefaultAsync(
                x => x.ChapterNumber == createChapterDto.ChapterNumber
                     && x.StandardId == createChapterDto.StandardId
                     && x.SubjectId == createChapterDto.SubjectId
                     && !x.IsDeleted
            );

            if (existingChapterByNumber != null)
                throw new UserFriendlyException(
                    $"Chapter number '{createChapterDto.ChapterNumber}' already exists in this subject and class."
                );

            var chapter = new Chapter(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                createChapterDto.StandardId,
                createChapterDto.SubjectId,
                createChapterDto.ChapterNumber,
                createChapterDto.Name,
                createChapterDto.StartDate,
                createChapterDto.EndDate,
                createChapterDto.Description
            );

            if (createChapterDto.Documents != null && createChapterDto.Documents.Any())
            {
                const int MaxFileSizeInBytes = 5 * 1024 * 1024;

                foreach (var doc in createChapterDto.Documents)
                {
                    byte[] fileBytes;
                    try
                    {
                        fileBytes = Convert.FromBase64String(doc.Base64Content);
                    }
                    catch (FormatException)
                    {
                        throw new UserFriendlyException("Invalid Base64 file content.");
                    }

                    if (fileBytes.Length > MaxFileSizeInBytes)
                        throw new UserFriendlyException("File size exceeds 5MB limit.");

                    var chapterDocument = new ChapterDocument(chapter.Id, doc.Base64Content);
                    chapter.AddDocument(chapterDocument);
                }
            }

            await chapterRepository.InsertAsync(chapter, autoSave: true);

            return ObjectMapper.Map<Chapter, ChapterrDto>(chapter);
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while creating chapter.");
            throw new UserFriendlyException("An unexpected error occurred while creating the chapter.");
        }
    }

    public async Task DeleteByIdChapterAsync(Guid id)
    {
        try
        {
            if (id == Guid.Empty)
                throw new UserFriendlyException("Invalid Chapter Id.");

            var queryable = await chapterRepository.GetQueryableAsync();

            var chapter = await queryable
                .Where(c => c.Id == id)
                .Include(c => c.Documents)
                .FirstOrDefaultAsync();

            if (chapter == null)
                throw new UserFriendlyException("Chapter not found.");

            if (chapter.Documents is { Count: > 0 })
            {
                foreach (var doc in chapter.Documents.ToList())
                {
                    await chapterDocumentRepository.DeleteAsync(doc, autoSave: true);
                }
            }

            await chapterRepository.HardDeleteAsync(chapter, autoSave: true);
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting chapter (hard delete).");
            throw new UserFriendlyException("Error deleting chapter.");
        }
    }

    public async Task DeleteByIdChapterDocumentAsync(Guid id)
    {
        try
        {
            if (id == Guid.Empty)
                throw new UserFriendlyException("Invalid Document Id.");

            var document = await chapterDocumentRepository.FindAsync(id);

            if (document == null)
                throw new UserFriendlyException("Chapter document not found.");

            var chapter = await chapterRepository.FirstOrDefaultAsync(c => c.Id == document.ChapterId && !c.IsDeleted);

            if (chapter != null && chapter.Documents != null)
            {
                var docInAggregate = chapter.Documents.FirstOrDefault(d => d.Id == document.Id);
                if (docInAggregate != null)
                {
                    chapter.RemoveDocument(docInAggregate);
                }
            }

            await chapterDocumentRepository.DeleteAsync(document, autoSave: true);
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting chapter document.");
            throw new UserFriendlyException("Error deleting chapter document.");
        }
    }

    public async Task<ChapterrDto> GetByIdChapterAsync(Guid id)
    {
        try
        {
            if (id == Guid.Empty)
                throw new UserFriendlyException("Invalid Chapter Id.");

            var chapter = await (await chapterRepository.GetQueryableAsync())
                .Where(x => x.Id == id && !x.IsDeleted)
                .Include(c => c.Documents)
                .FirstOrDefaultAsync();

            if (chapter == null)
                throw new UserFriendlyException("Chapter not found.");

            var chapterDto = ObjectMapper.Map<Chapter, ChapterrDto>(chapter);

            chapterDto.Documents = (chapter.Documents != null && chapter.Documents.Any())
                ? chapter.Documents.Select(d => new ChapterDocumentDto
                {
                    Base64Content = d.Base64Content
                }).ToList()
                : null;

            return chapterDto;
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while fetching chapter by id.");
            throw new UserFriendlyException("An unexpected error occurred while fetching the chapter.");
        }
    }

    public async Task<List<ChapterrDto>> GetListChaptersAsync(Guid standardId, Guid subjectId)
    {
        try
        {
            if (standardId == Guid.Empty)
                throw new UserFriendlyException("Invalid Standard Id.");
            if (subjectId == Guid.Empty)
                throw new UserFriendlyException("Invalid Subject Id.");

            var query = await chapterRepository.GetQueryableAsync();

            var chapters = await query
                .Where(x => x.StandardId == standardId &&
                            x.SubjectId == subjectId &&
                            !x.IsDeleted)
                .Include(c => c.Documents)
                .OrderBy(c => c.ChapterNumber)
                .ToListAsync();

            var chapterDtos = ObjectMapper.Map<List<Chapter>, List<ChapterrDto>>(chapters);

            var chaptersById = chapters.ToDictionary(c => c.Id);
            foreach (var dto in chapterDtos)
            {
                var docs = chaptersById[dto.Id].Documents;
                dto.Documents = (docs != null && docs.Any())
                    ? docs.Select(d => new ChapterDocumentDto
                    {
                        Base64Content = d.Base64Content
                    }).ToList()
                    : null;
            }

            return chapterDtos;
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while fetching chapter list.");
            throw new UserFriendlyException("An unexpected error occurred while fetching chapters.");
        }
    }

    public async Task<ChapterrDto> UpdateByIdChapterAsync(Guid id, UpdateChapterDto updateChapterDto)
    {
        try
        {
            if (id == Guid.Empty)
                throw new UserFriendlyException("Invalid Chapter Id.");

            var chapter = await chapterRepository
                .GetQueryableAsync()
                .Result
                .Where(c => c.Id == id && !c.IsDeleted)
                .Include(c => c.Documents)
                .FirstOrDefaultAsync();

            if (chapter == null)
                throw new UserFriendlyException("Chapter not found.");

            if (updateChapterDto.StartDate > updateChapterDto.EndDate)
                throw new UserFriendlyException("Start Date cannot be after End Date.");

            var existingChapterByName = await chapterRepository.FirstOrDefaultAsync(
                x => x.Id != id &&
                     x.Name.ToLower() == updateChapterDto.Name.ToLower() &&
                     x.StandardId == chapter.StandardId &&
                     x.SubjectId == chapter.SubjectId &&
                     !x.IsDeleted
            );

            if (existingChapterByName != null)
                throw new UserFriendlyException(
                    $"A chapter named '{updateChapterDto.Name}' already exists in this subject and class."
                );

            var existingChapterByNumber = await chapterRepository.FirstOrDefaultAsync(
                x => x.Id != id &&
                     x.ChapterNumber == updateChapterDto.ChapterNumber &&
                     x.StandardId == chapter.StandardId &&
                     x.SubjectId == chapter.SubjectId &&
                     !x.IsDeleted
            );

            if (existingChapterByNumber != null)
                throw new UserFriendlyException(
                    $"Chapter number '{updateChapterDto.ChapterNumber}' already exists in this subject and class."
                );

            chapter.ChapterNumber = updateChapterDto.ChapterNumber;
            chapter.Name = updateChapterDto.Name;
            chapter.StartDate = updateChapterDto.StartDate;
            chapter.EndDate = updateChapterDto.EndDate;
            chapter.Description = updateChapterDto.Description;

            if (updateChapterDto.Documents != null && updateChapterDto.Documents.Any())
            {
                if (chapter.Documents != null && chapter.Documents.Any())
                {
                    foreach (var doc in chapter.Documents.ToList())
                    {
                        await DeleteByIdChapterDocumentAsync(doc.Id);
                    }
                }

                const int MaxFileSizeInBytes = 5 * 1024 * 1024;

                foreach (var doc in updateChapterDto.Documents)
                {
                    byte[] fileBytes;
                    try
                    {
                        fileBytes = Convert.FromBase64String(doc.Base64Content);
                    }
                    catch (FormatException)
                    {
                        throw new UserFriendlyException("Invalid Base64 file content.");
                    }

                    if (fileBytes.Length > MaxFileSizeInBytes)
                        throw new UserFriendlyException("File size exceeds 5MB limit.");

                    var chapterDocument = new ChapterDocument(chapter.Id, doc.Base64Content);
                    chapter.AddDocument(chapterDocument);
                }
            }

            await chapterRepository.UpdateAsync(chapter, autoSave: true);

            return ObjectMapper.Map<Chapter, ChapterrDto>(chapter);
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while updating chapter.");
            throw new UserFriendlyException("An unexpected error occurred while updating the chapter.");
        }
    }
}
