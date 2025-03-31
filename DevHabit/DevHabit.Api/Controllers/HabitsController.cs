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
    public async Task<ActionResult<HabitsCollectionDto>> GetHabits()
    {
        //List<Habit> habits = await dbContext.Habits.ToListAsync();
        //return Ok(habits);
        //List<HabitDto> habits = await dbContext
        //    .Habits
        //    .Select(habit => new HabitDto
        //    {
        //        Id = habit.Id,
        //        Name = habit.Name,
        //        Description = habit.Description,
        //        Type = habit.Type,
        //        Frequency = new FrequencyDto
        //        {
        //            Type = habit.Frequency.Type,
        //            TimesPerPeriod = habit.Frequency.TimesPerPeriod
        //        },
        //        Target = new TargetDto
        //        {
        //            Value = habit.Target.Value,
        //            Unit = habit.Target.Unit
        //        },
        //        Status = habit.Status,
        //        IsArchived = habit.IsArchived,
        //        EndDate = habit.EndDate,
        //        Milestone = habit.Milestone == null
        //            ? null
        //            : new MilestoneDto
        //            {
        //                Target = habit.Milestone.Target,
        //                Current = habit.Milestone.Current
        //            },
        //        CreatedAtUtc = habit.CreatedAtUtc,
        //        UpdatedAtUtc = habit.UpdatedAtUtc,
        //        LastCompletedAtUtc = habit.LastCompletedAtUtc
        //    })
        //    .ToListAsync();


        //var habitsCollectionDto = new HabitsCollectionDto
        //{
        //    Data = habits
        //};

        //return Ok(habitsCollectionDto);
        List<HabitDto> habits = await dbContext
            .Habits
            .Select(HabitQueries.ProjectToDto())
            .ToListAsync();

        var habitsCollectionDto = new HabitsCollectionDto
        {
            Data = habits
        };

        return Ok(habitsCollectionDto);


    }

    [HttpGet("{id}")]
    public async Task<ActionResult<HabitDto?>> GetHabit(string id)
    {
        HabitDto? habit = await dbContext
            .Habits
            .Where(h => h.Id == id)
            .Select(HabitQueries.ProjectToDto())
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
