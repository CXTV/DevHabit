using Asp.Versioning;
using DevHabit.Api.Services;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Serialization;
using DevHabit.Api.Middleware;
using DevHabit.Api.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Npgsql;
using OpenTelemetry;
using DevHabit.Api.DTOs.Habits;
using DevHabit.Api.Entities;
using DevHabit.Api.Services.Sorting;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using DevHabit.Api.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Net.Http.Headers;
using DevHabit.Api.Jobs;
using Refit;
using Quartz;


namespace DevHabit.Api;

public static class DependencyInjection
{
    //服务添加啊
    public static WebApplicationBuilder AddApiServices(this WebApplicationBuilder builder)
    {
        //Contoller配置
        builder.Services.AddControllers(options =>
            {
                options.ReturnHttpNotAcceptable = true;
            })
            .AddNewtonsoftJson(options => options.SerializerSettings.ContractResolver =
                new CamelCasePropertyNamesContractResolver())
            .AddXmlSerializerFormatters();

        //自定义custom media type
        builder.Services.Configure<MvcOptions>(options =>
        {
            NewtonsoftJsonOutputFormatter formatter = options.OutputFormatters
                .OfType<NewtonsoftJsonOutputFormatter>()
                .First();

            formatter.SupportedMediaTypes.Add(CustomMediaTypeNames.Application.JsonV1);
            formatter.SupportedMediaTypes.Add(CustomMediaTypeNames.Application.JsonV2);
            formatter.SupportedMediaTypes.Add(CustomMediaTypeNames.Application.HateoasJson);
            formatter.SupportedMediaTypes.Add(CustomMediaTypeNames.Application.HateoasJsonV1);
            formatter.SupportedMediaTypes.Add(CustomMediaTypeNames.Application.HateoasJsonV2);
        });

        //api Versioning
        builder.Services
            .AddApiVersioning(
                options =>
                {
                    options.DefaultApiVersion = new ApiVersion(1, 0);
                    options.AssumeDefaultVersionWhenUnspecified = true;
                    options.ReportApiVersions = true;
                    options.ApiVersionSelector = new CurrentImplementationApiVersionSelector(options);
                    //options.ApiVersionReader = new UrlSegmentApiVersionReader();

                    //添加自定义的hateoas
                    options.ApiVersionReader = ApiVersionReader.Combine(
                        new MediaTypeApiVersionReader(),
                        new MediaTypeApiVersionReaderBuilder()
                            .Template("application/vnd.dev-habit.hateoas.{version}+json")
                            .Build());
                }
            )
            .AddMvc();

        //OpenApi
        builder.Services.AddOpenApi();

        //缓存
        builder.Services.AddResponseCaching();

        return builder;
    }

    //全局错误处理配置
    public static WebApplicationBuilder AddErrorHandling(this WebApplicationBuilder builder)
    {
        //配置ProblemDetails
        builder.Services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions.TryAdd("requestId", context.HttpContext.TraceIdentifier);
            };
        });

        //全局异常处理中间件服务注册
        builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

        return builder;

    }

    //数据库上下文服务注册
    public static WebApplicationBuilder AddDatabase(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options
                .UseNpgsql(
                    builder.Configuration.GetConnectionString("Database"),
                    npgsqlOptions => npgsqlOptions
                        .MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Application))
                .UseSnakeCaseNamingConvention());

        builder.Services.AddDbContext<ApplicationIdentityDbContext>(options =>
            options
                .UseNpgsql(
                    builder.Configuration.GetConnectionString("Database"),
                    npgsqlOptions => npgsqlOptions
                        .MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Identity))
                .UseSnakeCaseNamingConvention());

        return builder;
    }

    //注册OpenTelemetry
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
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

        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
        });

        return builder;
    }

    //集中注册服务
    public static WebApplicationBuilder AddApplicationServices(this WebApplicationBuilder builder)
    {
        //注册FluentValidation
        builder.Services.AddValidatorsFromAssemblyContaining<Program>();
        //注册SortMappingProvider
        builder.Services.AddTransient<SortMappingProvider>();
        //排序服务注册只有Habit
        builder.Services.AddSingleton<ISortMappingDefinition, SortMappingDefinition<HabitDto, Habit>>(_ =>
            HabitMappings.SortMapping);
        //注册数据整形服务
        builder.Services.AddTransient<DataShapingService>();
        //注册httpContextAccessor
        builder.Services.AddHttpContextAccessor();
        //注册Hateoas服务
        builder.Services.AddTransient<LinkService>();
        //注册TokenProvider
        builder.Services.AddTransient<TokenProvider>();

        //注册内存缓存
        builder.Services.AddMemoryCache();
        //注册UserContext
        builder.Services.AddScoped<UserContext>();

        //注册github外部服务
        builder.Services.AddScoped<GitHubAccessTokenService>();
        builder.Services.AddTransient<GitHubService>();

        // Global Resilience Handler
        builder.Services.AddHttpClient().ConfigureHttpClientDefaults(b => b.AddStandardResilienceHandler());

        builder.Services.AddHttpClient("github")
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://api.github.com");
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DevHabit", "1.0"));
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            });

        builder.Services.AddTransient<DelayHandler>();

        builder.Services
            .AddRefitClient<IGitHubApi>(new RefitSettings
            {
                ContentSerializer = new NewtonsoftJsonContentSerializer()
            })
            .ConfigureHttpClient(client => client.BaseAddress = new Uri("https://api.github.com"));


        // Encryption
        builder.Services.Configure<EncryptionOptions>(builder.Configuration.GetSection("Encryption"));
        builder.Services.AddTransient<EncryptionService>();

        // GitHub Automation
        builder.Services.Configure<GitHubAutomationOptions>
            (builder.Configuration.GetSection(GitHubAutomationOptions.SectionName));

        //Add ETag store
        builder.Services.AddSingleton<InMemoryETagStore>();

        return builder;
    }


    //注册身份验证服务
    public static WebApplicationBuilder AddAuthenticationServices(this WebApplicationBuilder builder)
    {

        builder.Services.AddIdentity<IdentityUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationIdentityDbContext>();

        builder.Services.Configure<JwtAuthOptions>(builder.Configuration.GetSection("Jwt"));

        JwtAuthOptions jwtAuthOptions = builder.Configuration.GetSection("Jwt").Get<JwtAuthOptions>()!;

        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwtAuthOptions.Issuer,
                    ValidAudience = jwtAuthOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtAuthOptions.Key))
                };
            });

        builder.Services.AddAuthorization();

        return builder;
    }

    //Quartz服务注册
    public static WebApplicationBuilder AddBackgroundJobs(this WebApplicationBuilder builder)
    {
        builder.Services.AddQuartz(q =>
        {
            q.AddJob<GitHubAutomationSchedulerJob>(options => options.WithIdentity("github-automation-scheduler"));

            q.AddTrigger(option => option
                .ForJob("github-automation-scheduler")
                .WithIdentity("github-automation-scheduler-trigger")
                .WithSimpleSchedule(schedule =>
                {
                    GitHubAutomationOptions settings = builder.Configuration
                        .GetSection(GitHubAutomationOptions.SectionName)
                        .Get<GitHubAutomationOptions>()!;

                    schedule.WithIntervalInMinutes(settings.ScanIntervalInMinutes)
                        .RepeatForever();
                })
            );
        });

        builder.Services.AddQuartzHostedService(options =>
            options.WaitForJobsToComplete = true);

        return builder;
    }

    //Cors
    public static WebApplicationBuilder AddCorsPolicy(this WebApplicationBuilder builder)
    {
        CorsOptions corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()!;

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(CorsOptions.PolicyName, policy =>
            {
                policy.WithOrigins(corsOptions.AllowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        return builder;
    }
}
