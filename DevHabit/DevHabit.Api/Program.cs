using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Habits;
using DevHabit.Api.DTOs.Tags;
using DevHabit.Api.Entities;
using DevHabit.Api.Extensions;
using DevHabit.Api.Middleware;
using DevHabit.Api.Services;
using DevHabit.Api.Services.Sorting;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Newtonsoft.Json.Serialization;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
    {
        options.ReturnHttpNotAcceptable = true;
    })
    .AddNewtonsoftJson(options => options.SerializerSettings.ContractResolver =
        new CamelCasePropertyNamesContractResolver())
    .AddXmlSerializerFormatters();

//�Զ���custom media type
builder.Services.Configure<MvcOptions>(options =>
{
    NewtonsoftJsonOutputFormatter formatter = options.OutputFormatters
        .OfType<NewtonsoftJsonOutputFormatter>()
        .First();
    formatter.SupportedMediaTypes.Add(CustomMediaTypeNames.Application.HateoasJson);
});


//ע��validator
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

//����ProblemDetails
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions.TryAdd("requestId", context.HttpContext.TraceIdentifier);
    };
});

//ȫ���쳣�����м������ע��
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddOpenApi();

//���ݿ������ķ���ע��
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options
        .UseNpgsql(
            builder.Configuration.GetConnectionString("Database"),
            npgsqlOptions => npgsqlOptions
                .MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Application))
        .UseSnakeCaseNamingConvention());

//Aspire����ע��
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(builder.Environment.ApplicationName))
    .WithTracing(tracing => tracing
        .AddHttpClientInstrumentation()
        .AddAspNetCoreInstrumentation()
        .AddNpgsql())
    .WithMetrics(metrics => metrics
        .AddHttpClientInstrumentation()
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation())
    .UseOtlpExporter();

//��־����ע��
builder.Logging.AddOpenTelemetry(options =>
{
    options.IncludeScopes = true;
    options.IncludeFormattedMessage = true;
});


//ע��SortMappingProvider
builder.Services.AddTransient<SortMappingProvider>();
//�������ע��ֻ��Habit
builder.Services.AddSingleton<ISortMappingDefinition, SortMappingDefinition<HabitDto, Habit>>(_ =>
    HabitMappings.SortMapping);

//ע���������η���
builder.Services.AddTransient<DataShapingService>();

//ע��httpContextAccessor
builder.Services.AddHttpContextAccessor();

//ע�����ӷ���
builder.Services.AddTransient<LinkService>();


WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.ApplyMigrations();
}

app.UseHttpsRedirection();

//�쳣�����м��ע��
app.UseExceptionHandler();


app.MapControllers();

await app.RunAsync();
