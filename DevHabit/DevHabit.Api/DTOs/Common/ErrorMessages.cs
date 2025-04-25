namespace DevHabit.Api.DTOs.Common;

public static class ErrorMessages
{
    public static string InvalidSort(string? sortValue)
    {
        return $"The provided sort parameter isn't valid: '{sortValue ?? "null"}'";
    }

    public static string InvalidFields(string? fieldsValue)
    {
        return $"The provided data shaping fields aren't valid: '{fieldsValue ?? "null"}'";
    }
}
