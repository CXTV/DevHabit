using Microsoft.AspNetCore.Mvc;

namespace DevHabit.Api.DTOs.Common;

public static  class ProblemDetailsFactories
{

    public static IActionResult InvalidSort(string? sortValue)
    {
        return new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Detail = $"The provided sort parameter isn't valid: '{sortValue}'"
        })
        {
            StatusCode = StatusCodes.Status400BadRequest
        };
    }

    public static IActionResult InvalidFields(string? fieldsValue)
    {
        return new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Detail = $"The provided data shaping fields aren't valid: '{fieldsValue}'"
        })
        {
            StatusCode = StatusCodes.Status400BadRequest
        };
    }
}
