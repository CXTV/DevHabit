using DevHabit.Api.DTOs.Common;
using DevHabit.Api.Entities;
using Microsoft.AspNetCore.Mvc;

namespace DevHabit.Api.DTOs.Habits;

public sealed record HabitsQueryParameters: AcceptHeaderDto
{
    [FromQuery(Name = "q")]
    public string? Search { get; set; }
    public HabitType? Type { get; init; }
    public HabitStatus? Status { get; init; }
    //排序
    public string? Sort { get; init; }
    //字段筛选
    public string? Fields { get; init; }
    //分页
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}
