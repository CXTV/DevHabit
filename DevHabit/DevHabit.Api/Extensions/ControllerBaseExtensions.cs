using Microsoft.AspNetCore.Mvc;

namespace DevHabit.Api.Extensions;

public static class ControllerBaseExtensions
{
    public static IActionResult InvalidSortProblem(this ControllerBase controller, string sortValue)
    {
        return controller.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            detail: $"The provided sort parameter isn't valid: '{sortValue}'");
    }

    public static IActionResult InvalidFieldsProblem(this ControllerBase controller, string fieldsValue)
    {
        return controller.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            detail: $"The provided data shaping fields aren't valid: '{fieldsValue}'");
    }
}
