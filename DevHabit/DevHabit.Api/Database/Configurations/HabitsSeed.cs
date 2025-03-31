using System.Collections.Generic;
using Bogus;
using Bogus.DataSets;
using DevHabit.Api.DTOs.Habits;
using DevHabit.Api.Entities;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Trace;

namespace DevHabit.Api.Database.Configurations;

public sealed class HabitsSeed
{
    public static void Seed(ApplicationDbContext context)
    {

        if (!context.Habits.Any())
        {
            string[] sports = new[]
            {
                "Football", "Basketball", "Tennis", "Soccer", "Baseball", "Running", "Cycling", "Swimming", "Boxing",
                "Hiking"
            };
            Randomizer.Seed = new Random(123);

            Faker<Habit> habitFaker = new Faker<Habit>()

                //.RuleFor(h => h.Id, f => $"h_{Guid.NewGuid().ToString()}") // 随机生成一个 GUID 作为 ID
                .RuleFor(h => h.Id, f => $"h_{Guid.CreateVersion7().ToString()}") // 随机生成一个 GUID 作为 ID
                .RuleFor(h => h.Name, f => f.PickRandom(sports)) // 随机生成一个爱好作为名称
                .RuleFor(h => h.Description, f => f.Lorem.Sentence()) // 随机生成一个描述
                .RuleFor(h => h.Type, f => f.PickRandom<HabitType>())
                .RuleFor(h => h.Frequency, f => new Frequency
                {
                    Type = f.PickRandom<FrequencyType>(), // 随机选择 0, 1, 2 或 3，并转换为 FrequencyType 枚举值
                    TimesPerPeriod = f.Random.Int(1, 7) // 随机生成一个次数
                })
                .RuleFor(h => h.Target, f => new Target
                {
                    Value = f.Random.Int(10, 100), // 随机生成目标值
                    Unit = "times" // 假设单位为 'times'
                })

                .RuleFor(h => h.Status, f => f.PickRandom<HabitStatus>()) 
                .RuleFor(h => h.IsArchived, f => f.Random.Bool())
                .RuleFor(h => h.CreatedAtUtc, f => f.Date.Recent(5).ToUniversalTime())
                .RuleFor(h => h.UpdatedAtUtc, (f, h) => h.CreatedAtUtc.AddDays(f.Random.Int(1, 10)))
                .RuleFor(h => h.LastCompletedAtUtc, (f, h) => h.UpdatedAtUtc?.AddDays(f.Random.Int(1, 5)))
                .RuleFor(h => h.EndDate, f => f.Date.FutureDateOnly(f.Random.Int(15,30)));



            //.RuleFor(h => h.UpdatedAtUtc, f => f.Date.Recent(10).ToUniversalTime()) 
            //.RuleFor(h => h.LastCompletedAtUtc, f => f.Date.Recent(5).ToUniversalTime()) 
            //.RuleFor(h => h.EndDate, f => f.Date.FutureDateOnly(1));

            List < Habit> allSeeds =  habitFaker.Generate(10);
            context.Habits.AddRange(allSeeds);
            context.SaveChanges();
        }
    }
}


