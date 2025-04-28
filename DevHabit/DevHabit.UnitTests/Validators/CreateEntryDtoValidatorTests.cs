using DevHabit.Api.DTOs.Entries;
using DevHabit.Api.Entities;
using FluentValidation.Results;

namespace DevHabit.UnitTests.Validators;

public class CreateEntryDtoValidatorTests
{
    private readonly CreateEntryDtoValidator _validator = new();

    [Fact] // 用来定义不带参数的独立测试
    public async Task Validate_ShouldSucceed_WhenInputDtoIsValid()
    {
        // Arrange
        var dto = new CreateEntryDto
        {
            HabitId = Habit.NewId(),
            Value = 1,
            Date = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        // Act
        ValidationResult validationResult = await _validator.ValidateAsync(dto);

        // Assert
        Assert.True(validationResult.IsValid);
        Assert.Empty(validationResult.Errors); //通过验证，错误列表为空
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenHabitIdIsEmpty()
    {
        // Arrange
        var dto = new CreateEntryDto
        {
            HabitId = string.Empty,
            Value = 1,
            Date = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        // Act
        ValidationResult validationResult = await _validator.ValidateAsync(dto);

        // Assert
        Assert.False(validationResult.IsValid); //验证失败
        ValidationFailure validationFailure = Assert.Single(validationResult.Errors); //检查错误列表里应该有且只有一个错误
        Assert.Equal(nameof(CreateEntryDto.HabitId), validationFailure.PropertyName); //验证这个错误是针对 HabitId 字段的
    }
}
