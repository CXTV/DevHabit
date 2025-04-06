using DevHabit.Api.DTOs.Common;

namespace DevHabit.Api.DTOs.Habits;

public sealed record HabitsCollectionDto: ICollectionResponse<HabitDto>
{
    public List<HabitDto> Items { get; init; }
}
