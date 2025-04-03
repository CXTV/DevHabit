﻿using DevHabit.Api.Entities;
using System.Linq.Expressions;
using DevHabit.Api.DTOs.Tags;

namespace DevHabit.Api.DTOs.Habits;

internal static class TagQueries
{
    public static Expression<Func<Tag, TagDto>> ProjectToDto()
    {
        return t => new TagDto
        {
            Id = t.Id,
            Name = t.Name,
            Description = t.Description,
            CreatedAtUtc = t.CreatedAtUtc,
            UpdatedAtUtc = t.UpdatedAtUtc
        };
    }
}
