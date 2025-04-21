using System.Dynamic;
using System.Net.Mime;
using Asp.Versioning;
using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Common;
using DevHabit.Api.DTOs.Habits;
using DevHabit.Api.Entities;
using DevHabit.Api.Services;
using DevHabit.Api.Services.Sorting;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevHabit.Api.Controllers;


[ApiController]
[Route("habits")]
[Authorize(Roles = Roles.Member)]
[ApiVersion(1.0)]
[Authorize]
[Produces(
    MediaTypeNames.Application.Json,
    CustomMediaTypeNames.Application.JsonV1,
    CustomMediaTypeNames.Application.JsonV2,
    CustomMediaTypeNames.Application.HateoasJson,
    CustomMediaTypeNames.Application.HateoasJsonV1,
    CustomMediaTypeNames.Application.HateoasJsonV2)]
public sealed class HabitsController(
    ApplicationDbContext dbContext,
    LinkService linkService,
    UserContext userContext
    ) :ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<HabitsCollectionDto>> GetHabits(
        [FromQuery] HabitsQueryParameters query,
        SortMappingProvider sortMappingProvider,
        DataShapingService dataShapingService)
    {
        //获取当前用户的Id
        string? userId = await userContext.GetUserIdAsync();

        //判断Sort参数是否合法
        if (!sortMappingProvider.ValidateMappings<HabitDto, Habit>(query.Sort))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The provided sort parameter isn't valid: '{query.Sort}'");
        }


        //判断前端传入的Fields参数是否合法
        if (!dataShapingService.Validate<HabitDto>(query.Fields))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The provided data shaping fields aren't valid: '{query.Fields}'");
        }

        //全部小写，去空格
        query.Search = query.Search?.Trim().ToLower();

        //获取SORT Dto和Entity的映射关系
        SortMapping[] sortMappings = sortMappingProvider.GetMappings<HabitDto, Habit>();

        //return Ok(habitsCollectionDto);未使用分页
        //List<HabitDto> habits = await dbContext
        //        .Habits
        //        //只能通过Name和Description进行模糊查询
        //        .Where(h=> query.Search == null ||
        //                   h.Name.ToLower().Contains(query.Search) ||
        //                   h.Description != null && h.Description.ToLower().Contains(query.Search))
        //        .Where(h=> query.Type == null || h.Type == query.Type)
        //        .Where(h=>query.Status==null || h.Status == query.Status)
        //        //动态排序
        //        .ApplySort(query.Sort, sortMappings)
        //        .Select(HabitQueries.ProjectToDto())
        //        .ToListAsync();


        IQueryable<HabitDto> habitsQuery = dbContext
            .Habits
            .Where(h => h.UserId == userId)
            .Where(h => query.Search == null ||
                        h.Name.ToLower().Contains(query.Search) ||
                        h.Description != null && h.Description.ToLower().Contains(query.Search))
            .Where(h => query.Type == null || h.Type == query.Type)
            .Where(h => query.Status == null || h.Status == query.Status)
            .ApplySort(query.Sort, sortMappings)
            .Select(HabitQueries.ProjectToDto());


        int totalCount = await habitsQuery.CountAsync();

        List<HabitDto> habits = await habitsQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();


        //判断是否需要返回链接
        bool includeLinks = query.Accept == CustomMediaTypeNames.Application.HateoasJson;

        var paginationResult = new PaginationResult<ExpandoObject>
        {
            Items = dataShapingService.ShapeCollectionData(
                habits, 
                query.Fields,
                includeLinks? h => CreateLinksForHabit(h.Id, query.Fields):null),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
        
        //有就返回带link的分页结果
        if (includeLinks)
        {
            //创建Link
            paginationResult.Links = CreateLinksForHabits(
                query,
                paginationResult.HasNextPage,
                paginationResult.HasPreviousPage);
        }

        return Ok(paginationResult);
    }


    [HttpGet("{id}")]
    [MapToApiVersion(1.0)]
    public async Task<ActionResult<HabitWithTagsDto>> GetHabit(
        string id,
        [FromQuery] HabitsQueryParameters query,
        DataShapingService dataShapingService)
    {

        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        if (!dataShapingService.Validate<HabitWithTagsDto>(query.Fields))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The provided data shaping fields aren't valid: '{query.Fields}'");
        }
        
        HabitWithTagsDto? habit = await dbContext
            .Habits
            .Where(h => h.Id == id && h.UserId == userId)
            .Select(HabitQueries.ProjectToDtoWithTags())
            .FirstOrDefaultAsync();

        if (habit is null)
        {
            return NotFound();
        }

        ExpandoObject shapedHabitDto = dataShapingService.ShapeData(habit, query.Fields);

        //判断是否需要返回链接
        if (query.IncludeLinks)
        {
            List<LinkDto> links = CreateLinksForHabit(id, query.Fields);

            shapedHabitDto.TryAdd("links", links);
        }

        return Ok(shapedHabitDto);

    }


    [HttpGet("{id}")]
    [ApiVersion(2.0)]
    public async Task<IActionResult> GetHabitV2(
        string id,
        [FromQuery] HabitsQueryParameters query,
        DataShapingService dataShapingService)
    {

        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        if (!dataShapingService.Validate<HabitWithTagsDtoV2>(query.Fields))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"The provided data shaping fields aren't valid: '{query.Fields}'");
        }

        HabitWithTagsDtoV2? habit = await dbContext
            .Habits
            .Where(h => h.Id == id && h.UserId == userId)
            .Select(HabitQueries.ProjectToDtoWithTagsV2())
            .FirstOrDefaultAsync();

        if (habit is null)
        {
            return NotFound();
        }

        ExpandoObject shapedHabitDto = dataShapingService.ShapeData(habit, query.Fields);

        if (query.IncludeLinks)
        {
            List<LinkDto> links = CreateLinksForHabit(id, query.Fields);

            shapedHabitDto.TryAdd("links", links);
        }

        return Ok(shapedHabitDto);
    }


    [HttpPost]
    public async Task<ActionResult<HabitDto>> CreateHabit(
        CreateHabitDto createHabitDto,
        [FromHeader] AcceptHeaderDto acceptHeader,
        IValidator<CreateHabitDto> validator
        )
    {
        //获取当前用户的Id
        string? userId = await userContext.GetUserIdAsync();
        //判断用户是否登录
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        //await validator.ValidateAndThrowAsync(createHabitDto);

        ValidationResult validationResult = await validator.ValidateAsync(createHabitDto);

        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.ToDictionary());
        }

        Habit habit = createHabitDto.ToEntity(userId);

        dbContext.Habits.Add(habit);

        await dbContext.SaveChangesAsync();

        HabitDto habitDto = habit.ToDto();

        habitDto.Links = CreateLinksForHabit(habitDto.Id, null);

        if (acceptHeader.IncludeLinks) habitDto.Links = CreateLinksForHabit(habit.Id, null);


        return CreatedAtAction(nameof(GetHabit), new { id = habitDto.Id }, habitDto);

    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateHabit(string id, UpdateHabitDto updateHabitDto)
    {

        string? userId = await userContext.GetUserIdAsync();

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        Habit? habit = await dbContext.Habits.FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId);

        if (habit is null)
        {
            return NotFound();
        }

        habit.UpdateFromDto(updateHabitDto);

        await dbContext.SaveChangesAsync();

        return NoContent();
    }



    [HttpPatch("{id}")]
    public async Task<ActionResult> PatchHabit(string id, JsonPatchDocument<HabitDto> patchDocument)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }
        Habit? habit = await dbContext.Habits.FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId);

        if (habit is null)
        {
            return NotFound();
        }

        HabitDto habitDto = habit.ToDto();

        patchDocument.ApplyTo(habitDto, ModelState);

        if (!TryValidateModel(habitDto))
        {
            return ValidationProblem(ModelState);
        }

        habit.Name = habitDto.Name;
        habit.Description = habitDto.Description;
        habit.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        return NoContent();
    }


    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteHabit(string id)
    {
        string? userId = await userContext.GetUserIdAsync();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        Habit? habit = await dbContext.Habits.FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId);

        if (habit is null)
        {
            return NotFound();
        }

        dbContext.Habits.Remove(habit);

        await dbContext.SaveChangesAsync();

        return NoContent();
    }



    //添加带page的链接
    private List<LinkDto> CreateLinksForHabits(
        HabitsQueryParameters parameters,
        bool hasNextPage,
        bool hasPreviousPage)
    {
        List<LinkDto> links =
        [
            linkService.Create(nameof(GetHabits), "self", HttpMethods.Get, new
            {
                page = parameters.Page,
                pageSize = parameters.PageSize,
                fields = parameters.Fields,
                q = parameters.Search,
                sort = parameters.Sort,
                type = parameters.Type,
                status = parameters.Status
            }),
            linkService.Create(nameof(CreateHabit), "create", HttpMethods.Post)
        ];

        if (hasNextPage)
        {
            links.Add(linkService.Create(nameof(GetHabits), "next-page", HttpMethods.Get, new
            {
                page = parameters.Page + 1,
                pageSize = parameters.PageSize,
                fields = parameters.Fields,
                q = parameters.Search,
                sort = parameters.Sort,
                type = parameters.Type,
                status = parameters.Status
            }));
        }

        if (hasPreviousPage)
        {
            links.Add(linkService.Create(nameof(GetHabits), "previous-page", HttpMethods.Get, new
            {
                page = parameters.Page - 1,
                pageSize = parameters.PageSize,
                fields = parameters.Fields,
                q = parameters.Search,
                sort = parameters.Sort,
                type = parameters.Type,
                status = parameters.Status
            }));
        }

        return links;
    }


    private List<LinkDto> CreateLinksForHabit(string id, string? fields)
    {
        List<LinkDto> links =
        [
            linkService.Create(nameof(GetHabit), "self", HttpMethods.Get, new { id, fields }),
            linkService.Create(nameof(UpdateHabit), "update", HttpMethods.Put, new { id }),
            linkService.Create(nameof(PatchHabit), "partial-update", HttpMethods.Patch, new { id }),
            linkService.Create(nameof(DeleteHabit), "delete", HttpMethods.Delete, new { id }),
            linkService.Create(
                nameof(HabitTagsController.UpsertHabitTags),
                "upsert-tags",
                HttpMethods.Put,
                new { habitId = id },
                HabitTagsController.Name)
        ];
        return links;
    }
}



