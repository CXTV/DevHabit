﻿using Asp.Versioning;
using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Common;
using DevHabit.Api.DTOs.Entries;
using DevHabit.Api.Entities;
using DevHabit.Api.Services;
using DevHabit.Api.Services.Sorting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Dynamic;
using System.Net.Mime;
using FluentValidation;

namespace DevHabit.Api.Controllers;

[EnableRateLimiting("default")]
[Authorize(Roles = Roles.Member)]
[ApiController]
[Route("entries")]
[ApiVersion(1.0)]
[Produces(
    MediaTypeNames.Application.Json,
    CustomMediaTypeNames.Application.JsonV1,
    CustomMediaTypeNames.Application.HateoasJson,
    CustomMediaTypeNames.Application.HateoasJsonV1)]
public sealed class EntriesController(
    ApplicationDbContext dbContext,
    LinkService linkService,
    UserContext userContext) : ControllerBase
{

    [HttpGet]
    public async Task<IActionResult> GetEntries(
    [FromQuery] EntriesQueryParameters query,
    SortMappingProvider sortMappingProvider,
    DataShapingService dataShapingService)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        if (!sortMappingProvider.ValidateMappings<EntryDto, Entry>(query.Sort))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: ErrorMessages.InvalidSort(query.Sort));
        }

        if (!dataShapingService.Validate<EntryDto>(query.Fields))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: ErrorMessages.InvalidFields(query.Fields));
        }

        SortMapping[] sortMappings = sortMappingProvider.GetMappings<EntryDto, Entry>();

        IQueryable<Entry> entriesQuery = dbContext.Entries
            .Where(e => e.UserId == userId)
            .Where(e => query.HabitId == null || e.HabitId == query.HabitId)
            .Where(e => query.FromDate == null || e.Date >= query.FromDate)
            .Where(e => query.ToDate == null || e.Date <= query.ToDate)
            .Where(e => query.Source == null || e.Source == query.Source)
            .Where(e => query.IsArchived == null || e.IsArchived == query.IsArchived);

        int totalCount = await entriesQuery.CountAsync();

        List<EntryDto> entries = await entriesQuery
            .ApplySort(query.Sort, sortMappings)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(EntryQueries.ProjectToDto())
            .ToListAsync();

        var paginationResult = new PaginationResult<ExpandoObject>
        {
            Items = dataShapingService.ShapeCollectionData(
                entries,
                query.Fields,
                query.IncludeLinks ? e => CreateLinksForEntry(e.Id, query.Fields, e.IsArchived) : null),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };

        if (query.IncludeLinks)
        {
            paginationResult.Links = CreateLinksForEntries(
                query,
                paginationResult.HasNextPage,
                paginationResult.HasPreviousPage);
        }

        return Ok(paginationResult);
    }


    //游标分页
    [HttpGet("cursor")]
    public async Task<IActionResult> GetEntriesCursor(
        [FromQuery] EntriesCursorQueryParameters query,
        DataShapingService dataShapingService)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        if (!dataShapingService.Validate<EntryDto>(query.Fields))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: ErrorMessages.InvalidFields(query.Fields));
        }

        IQueryable<Entry> entriesQuery = dbContext.Entries
            .Where(e => e.UserId == userId)
            .Where(e => query.HabitId == null || e.HabitId == query.HabitId)
            .Where(e => query.FromDate == null || e.Date >= query.FromDate)
            .Where(e => query.ToDate == null || e.Date <= query.ToDate)
            .Where(e => query.Source == null || e.Source == query.Source)
            .Where(e => query.IsArchived == null || e.IsArchived == query.IsArchived);

        //如果传入了游标，则根据游标进行查询
        if (!string.IsNullOrWhiteSpace(query.Cursor)) 
        {
            //解码当前游标
            var cursor = EntryCursorDto.Decode(query.Cursor);

            //根据游标的日期和 ID 进行过滤
            if (cursor is not null)
            {
                //根据解码的信息进行数据过滤
                entriesQuery = entriesQuery.Where(e =>
                    e.Date < cursor.Date ||
                    e.Date == cursor.Date && string.Compare(e.Id, cursor.Id) <= 0);
            }
        }

        List<EntryDto> entries = await entriesQuery
            .OrderByDescending(e => e.Date)                // 先按日期倒序排序（最新的在前）
            .ThenByDescending(e => e.Id)                   // 如果日期一样，再按 Id 倒序排序，保证排序唯一性
            .Take(query.Limit + 1)                         // 取比请求数量多一条的数据（用于判断是否还有下一页）
            .Select(EntryQueries.ProjectToDto())           // 将 Entry 实体映射为 EntryDto（通常用于简化返回数据）
            .ToListAsync();                                // 异步执行查询并转成 List<EntryDto>

        //如果拿到的数据大于Limit，说明还有下一页
        bool hasNextPage = entries.Count > query.Limit;
        string? nextCursor = null;
        // 如果有下一页，则取最后一条记录的 Id 和 Date 作为下一个游标
        if (hasNextPage)
        {
            EntryDto lastEntry = entries[^1]; // 取最后一条记录
            nextCursor = EntryCursorDto.Encode(lastEntry.Id, lastEntry.Date); // 编码为下一个游标
            entries.RemoveAt(entries.Count - 1); // 把多出来的那条去掉，只返回 Limit 条
        }

        var paginationResult = new CollectionResponse<ExpandoObject>
        {
            Items = dataShapingService.ShapeCollectionData(
                entries,
                query.Fields,
                query.IncludeLinks ? e => CreateLinksForEntry(e.Id, query.Fields, e.IsArchived) : null)
        };

        if (query.IncludeLinks)
        {
            paginationResult.Links = CreateLinksForEntriesCursor(
                query,
                nextCursor);
        }

        return Ok(paginationResult);
    }


    [HttpGet("{id}")]
    public async Task<IActionResult> GetEntry(
        string id,
        [FromQuery] EntryQueryParameters query,
        DataShapingService dataShapingService)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        if (!dataShapingService.Validate<EntryDto>(query.Fields))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: ErrorMessages.InvalidFields(query.Fields));
        }

        EntryDto? entry = await dbContext.Entries
            .Where(e => e.Id == id && e.UserId == userId)
            .Select(EntryQueries.ProjectToDto())
            .FirstOrDefaultAsync();

        if (entry is null)
        {
            return NotFound();
        }

        ExpandoObject shapedEntryDto = dataShapingService.ShapeData(entry, query.Fields);

        if (query.IncludeLinks)
        {
            ((IDictionary<string, object?>)shapedEntryDto)[nameof(ILinksResponse.Links)] =
                CreateLinksForEntry(id, query.Fields, entry.IsArchived);
        }

        return Ok(shapedEntryDto);
    }

    [HttpPost]
    [IdempotentRequest]
    public async Task<ActionResult<EntryDto>> CreateEntry(
        CreateEntryDto createEntryDto,
        [FromHeader] AcceptHeaderDto acceptHeader,
        IValidator<CreateEntryDto> validator)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        await validator.ValidateAndThrowAsync(createEntryDto);

        Habit? habit = await dbContext.Habits
            .FirstOrDefaultAsync(h => h.Id == createEntryDto.HabitId && h.UserId == userId);

        if (habit is null)
        {
            return Problem(
                detail: $"Habit with ID '{createEntryDto.HabitId}' does not exist.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        Entry entry = createEntryDto.ToEntity(userId, habit);
        dbContext.Entries.Add(entry);
        await dbContext.SaveChangesAsync();

        EntryDto entryDto = entry.ToDto();

        if (acceptHeader.IncludeLinks)
        {
            entryDto.Links = CreateLinksForEntry(entry.Id, null, entry.IsArchived);
        }

        return CreatedAtAction(nameof(GetEntry), new { id = entryDto.Id }, entryDto);
    }



    [HttpPost("batch")]
    public async Task<ActionResult<List<EntryDto>>> CreateEntryBatch(
        CreateEntryBatchDto createEntryBatchDto,
        [FromHeader] AcceptHeaderDto acceptHeader,
        IValidator<CreateEntryBatchDto> validator)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        await validator.ValidateAndThrowAsync(createEntryBatchDto);

        //收集所有 entry 中使用到的 HabitId，防止重复
        var habitIds = createEntryBatchDto.Entries
            .Select(e => e.HabitId)
            .ToHashSet();

        //从数据库中查询这些习惯是否属于当前用户
        List<Habit> existingHabits = await dbContext.Habits
            .Where(h => habitIds.Contains(h.Id) && h.UserId == userId)
            .ToListAsync();

        //如果查询到的习惯数量和传入的习惯ID数量不一致，说明有无效的习惯ID
        if (existingHabits.Count != habitIds.Count)
        {
            return Problem(
                detail: "One or more habit IDs is invalid",
                statusCode: StatusCodes.Status400BadRequest);
        }

        //将 DTO 转换为 Entity 对象，准备写入数据库
        var entries = createEntryBatchDto.Entries
            .Select(dto => dto.ToEntity(userId, existingHabits.First(h => h.Id == dto.HabitId)))
            .ToList();

        dbContext.Entries.AddRange(entries);
        await dbContext.SaveChangesAsync();

        //把 Entity 转换为返回给前端的 DTO
        var entryDtos = entries.Select(e => e.ToDto()).ToList();

        if (acceptHeader.IncludeLinks)
        {
            foreach (EntryDto entryDto in entryDtos)
            {
                entryDto.Links = CreateLinksForEntry(entryDto.Id, null, entryDto.IsArchived);
            }
        }

        return CreatedAtAction(nameof(GetEntries), entryDtos);
    }


    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateEntry(
        string id,
        UpdateEntryDto updateEntryDto,
        IValidator<UpdateEntryDto> validator)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        await validator.ValidateAndThrowAsync(updateEntryDto);

        Entry? entry = await dbContext.Entries
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

        if (entry is null)
        {
            return NotFound();
        }

        entry.UpdateFromDto(updateEntryDto);
        await dbContext.SaveChangesAsync();

        return NoContent();
    }


    [HttpPut("{id}/archive")]
    public async Task<ActionResult> ArchiveEntry(string id)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        Entry? entry = await dbContext.Entries
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

        if (entry is null)
        {
            return NotFound();
        }

        entry.IsArchived = true;
        entry.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return NoContent();
    }


    [HttpPut("{id}/un-archive")]
    public async Task<ActionResult> UnArchiveEntry(string id)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        Entry? entry = await dbContext.Entries
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

        if (entry is null)
        {
            return NotFound();
        }

        entry.IsArchived = false;
        entry.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return NoContent();
    }


    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteEntry(string id)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        Entry? entry = await dbContext.Entries
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

        if (entry is null)
        {
            return NotFound();
        }

        dbContext.Entries.Remove(entry);
        await dbContext.SaveChangesAsync();

        return NoContent();
    }


    [HttpGet("stats")]
    public async Task<ActionResult<EntryStatsDto>> GetStats()
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var entries = await dbContext.Entries
            .Where(e => e.UserId == userId)
            .OrderBy(e => e.Date)
            .Select(e => new { e.Date })
            .ToListAsync();

        if (!entries.Any())
        {
            return Ok(new EntryStatsDto
            {
                DailyStats = [],
                TotalEntries = 0,
                CurrentStreak = 0,
                LongestStreak = 0
            });
        }

        // Calculate daily stats
        var dailyStats = entries
            .GroupBy(e => e.Date)
            .Select(g => new DailyStatsDto
            {
                Date = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(s => s.Date)
            .ToList();

        // Calculate total entries
        int totalEntries = entries.Count;

        // Calculate streaks
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dates = entries.Select(e => e.Date).Distinct().OrderBy(d => d).ToList();

        int currentStreak = 0;
        int longestStreak = 0;
        int currentCount = 0;

        // Calculate current streak (must be active up to today)
        for (int i = dates.Count - 1; i >= 0; i--)
        {
            if (i == dates.Count - 1)
            {
                if (dates[i] == today)
                {
                    currentStreak = 1;
                }
                else
                {
                    break;
                }
            }
            else if (dates[i].AddDays(1) == dates[i + 1])
            {
                currentStreak++;
            }
            else
            {
                break;
            }
        }

        // Calculate longest streak
        for (int i = 0; i < dates.Count; i++)
        {
            if (i == 0 || dates[i] == dates[i - 1].AddDays(1))
            {
                currentCount++;
                longestStreak = Math.Max(longestStreak, currentCount);
            }
            else
            {
                currentCount = 1;
            }
        }

        return Ok(new EntryStatsDto
        {
            DailyStats = dailyStats,
            TotalEntries = totalEntries,
            CurrentStreak = currentStreak,
            LongestStreak = longestStreak
        });
    }


    //用于生成带游标的Links
    private List<LinkDto> CreateLinksForEntriesCursor(
        EntriesCursorQueryParameters parameters,
        string? nextCursor)
    {
        List<LinkDto> links =
        [
            linkService.Create(nameof(GetEntriesCursor), "self", HttpMethods.Get, new
            {
                cursor = parameters.Cursor,
                limit = parameters.Limit,
                fields = parameters.Fields,
                habitId = parameters.HabitId,
                fromDate = parameters.FromDate,
                toDate = parameters.ToDate,
                source = parameters.Source,
                isArchived = parameters.IsArchived
            }),
            linkService.Create(nameof(GetStats), "stats", HttpMethods.Get),
            linkService.Create(nameof(CreateEntry), "create", HttpMethods.Post),
            linkService.Create(nameof(CreateEntryBatch), "create-batch", HttpMethods.Post)
        ];

        //如果有下一页，则添加下一页的链接
        if (!string.IsNullOrWhiteSpace(nextCursor))
        {
            links.Add(linkService.Create(nameof(GetEntriesCursor), "next-page", HttpMethods.Get, new
            {
                cursor = nextCursor,
                limit = parameters.Limit,
                fields = parameters.Fields,
                habitId = parameters.HabitId,
                fromDate = parameters.FromDate,
                toDate = parameters.ToDate,
                source = parameters.Source,
                isArchived = parameters.IsArchived
            }));
        }

        return links;
    }


    private List<LinkDto> CreateLinksForEntries(
    EntriesQueryParameters parameters,
    bool hasNextPage,
    bool hasPreviousPage)
    {
        List<LinkDto> links =
        [
            linkService.Create(nameof(GetEntries), "self", HttpMethods.Get, new
            {
                page = parameters.Page,
                pageSize = parameters.PageSize,
                fields = parameters.Fields,
                sort = parameters.Sort,
                habitId = parameters.HabitId,
                fromDate = parameters.FromDate,
                toDate = parameters.ToDate,
                source = parameters.Source,
                isArchived = parameters.IsArchived
            }),

            linkService.Create(nameof(GetStats), "stats", HttpMethods.Get),
            linkService.Create(nameof(CreateEntry), "create", HttpMethods.Post),
            linkService.Create(nameof(CreateEntryBatch), "create-batch", HttpMethods.Post)
        ];

        if (hasNextPage)
        {
            links.Add(linkService.Create(nameof(GetEntries), "next-page", HttpMethods.Get, new
            {
                page = parameters.Page + 1,
                pageSize = parameters.PageSize,
                fields = parameters.Fields,
                sort = parameters.Sort,
                habitId = parameters.HabitId,
                fromDate = parameters.FromDate,
                toDate = parameters.ToDate,
                source = parameters.Source,
                isArchived = parameters.IsArchived
            }));
        }

        if (hasPreviousPage)
        {
            links.Add(linkService.Create(nameof(GetEntries), "previous-page", HttpMethods.Get, new
            {
                page = parameters.Page - 1,
                pageSize = parameters.PageSize,
                fields = parameters.Fields,
                sort = parameters.Sort,
                habitId = parameters.HabitId,
                fromDate = parameters.FromDate,
                toDate = parameters.ToDate,
                source = parameters.Source,
                isArchived = parameters.IsArchived
            }));
        }

        return links;
    }


    private List<LinkDto> CreateLinksForEntry(string id, string? fields, bool isArchived)
    {
        List<LinkDto> links =
        [
            linkService.Create(nameof(GetEntry), "self", HttpMethods.Get, new { id, fields }),
            linkService.Create(nameof(UpdateEntry), "update", HttpMethods.Put, new { id }),
            isArchived ?
                linkService.Create(nameof(UnArchiveEntry), "un-archive", HttpMethods.Put, new { id }) :
                linkService.Create(nameof(ArchiveEntry), "archive", HttpMethods.Put, new { id }),
            linkService.Create(nameof(DeleteEntry), "delete", HttpMethods.Delete, new { id })
        ];

        return links;
    }

}
