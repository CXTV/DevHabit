using CsvHelper.Configuration.Attributes;

namespace DevHabit.Api.DTOs.EntryImports;

public sealed class CsvEntryRecord
{
    [Name("habit_id")]
    public required string HabitId { get; init; } //CSV 文件里叫 "habit_id" 的列，绑定到 HabitId 这个属性。

    [Name("date")]
    public required DateOnly Date { get; init; } //CSV 文件里叫 "date" 的列，绑定到 Date 这个属性。

    [Name("notes")]
    public string? Notes { get; init; } //CSV 文件里叫 "notes" 的列，绑定到 Notes 这个属性。
}
