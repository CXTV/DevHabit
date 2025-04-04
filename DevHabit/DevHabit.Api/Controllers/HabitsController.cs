﻿using System.Linq.Expressions;
using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Habits;
using DevHabit.Api.Entities;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevHabit.Api.Controllers;

[ApiController]
[Route("habits")]
public sealed class HabitsController(ApplicationDbContext dbContext):ControllerBase
{


    [HttpGet]
    public async Task<ActionResult<HabitsCollectionDto>> GetHabits([FromQuery] HabitsQueryParameters query)
    {

        query.Search = query.Search?.Trim().ToLower();

        //return Ok(habitsCollectionDto);
        List<HabitDto> habits = await dbContext
            .Habits
            .Where(h=> query.Search == null ||
                       h.Name.ToLower().Contains(query.Search) ||
                       h.Description != null && h.Description.ToLower().Contains(query.Search))
            .Where(h=> query.Type == null || h.Type == query.Type)
            .Where(h=>query.Status==null || h.Status == query.Status)
            .Select(HabitQueries.ProjectToDto())
            .ToListAsync();

        var habitsCollectionDto = new HabitsCollectionDto
        {
            Data = habits
        };

        return Ok(habitsCollectionDto);
    }


    //[HttpGet("{id}")]
    //public async Task<ActionResult<HabitDto?>> GetHabit(string id)
    //{
    //    //HabitDto? habit = await dbContext
    //    //    .Habits
    //    //    .Where(h => h.Id == id)
    //    //    .Select(HabitQueries.ProjectToDto())
    //    //    .FirstOrDefaultAsync();

    //    //if (habit is null)
    //    //{
    //    //    return NotFound();
    //    //}

    //    //return Ok(habit);
    //    Habit habitEntity = await dbContext.Habits.FindAsync(id);

    //    if (habitEntity is null)
    //    {
    //        return NotFound();
    //    }

    //    HabitDto habitDto = habitEntity.ToDto(); // 这里用扩展方法转换
    //    return Ok(habitDto);
    //}

    [HttpGet("{id}")]
    public async Task<ActionResult<HabitWithTagsDto>> GetHabit(string id)
    {

        HabitWithTagsDto? habit = await dbContext
            .Habits
            .Where(h => h.Id == id)
            .Select(HabitQueries.ProjectToDtoWithTags())
            .FirstOrDefaultAsync();

        if (habit is null)
        {
            return NotFound();
        }


        return Ok(habit);

    }



    [HttpPost]
    public async Task<ActionResult<HabitDto>> CreateHabit(CreateHabitDto createhabitDto)
    {
        Habit habit = createhabitDto.ToEntity();

        dbContext.Habits.Add(habit);

        await dbContext.SaveChangesAsync();

        HabitDto habitDto = habit.ToDto();

        return CreatedAtAction(nameof(GetHabit), new { id = habitDto.Id }, habitDto);

    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateHabit(string id, UpdateHabitDto updateHabitDto)
    {
        Habit? habit = await dbContext.Habits.FirstOrDefaultAsync(h => h.Id == id);

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
        Habit? habit = await dbContext.Habits.FirstOrDefaultAsync(h => h.Id == id);

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
        Habit? habit = await dbContext.Habits.FirstOrDefaultAsync(h => h.Id == id);

        if (habit is null)
        {
            return NotFound();
        }

        dbContext.Habits.Remove(habit);

        await dbContext.SaveChangesAsync();

        return NoContent();
    }
}
