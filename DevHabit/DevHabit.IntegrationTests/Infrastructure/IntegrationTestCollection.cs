namespace DevHabit.IntegrationTests.Infrastructure;

//让多个测试类能共享同一个 DevHabitWebAppFactory
[CollectionDefinition(nameof(IntegrationTestCollection))]
public sealed class IntegrationTestCollection : ICollectionFixture<DevHabitWebAppFactory>;
