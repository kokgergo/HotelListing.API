using HotelListing.API.Core.Configuration;
using HotelListing.API.Core.Contracts;
using HotelListing.API.Data;
using HotelListing.API.Core.Middleware;
using HotelListing.API.Core.Repository;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.AspNetCore.OData;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connString = builder.Configuration.GetConnectionString("HotelListingDbConnectionString");

builder.Services.AddDbContext<HotelListingDbContext>(options => { options.UseSqlServer(connString); });

builder.Services.AddIdentityCore<ApiUser>()
        .AddRoles<IdentityRole>()
        .AddTokenProvider<DataProtectorTokenProvider<ApiUser>>("HotelListingAPI")
        .AddEntityFrameworkStores<HotelListingDbContext>()
        .AddDefaultTokenProviders();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options=>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "Hotel Listing API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = @"JWT Authorization heder",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = JwtBearerDefaults.AuthenticationScheme
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = JwtBearerDefaults.AuthenticationScheme
                },
                Scheme = "Oauth2",
                Name = JwtBearerDefaults.AuthenticationScheme,
                In = ParameterLocation.Header

            },
            new List<string>()
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", b => b.AllowAnyHeader()
                                        .AllowAnyOrigin()
                                        .AllowAnyMethod());
});
builder.Services.AddApiVersioning(options =>
{
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new QueryStringApiVersionReader("api-version"),
        new HeaderApiVersionReader("X-Version"),
        new MediaTypeApiVersionReader("ver")
        );
});
builder.Services.AddVersionedApiExplorer(
    options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });
builder.Host.UseSerilog((ctx, lc) => lc.WriteTo.Console().ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddAutoMapper(typeof(MapperConfig));

builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<ICountriesRepository, CountriesRepository>();
builder.Services.AddScoped<IHotelsRepository, HotelsRepository>();
builder.Services.AddScoped<IAuthManager,  AuthManager>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme; //"Bearer"
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtSettings:Audiance"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:Key"]))
    };
});

builder.Services.AddResponseCaching(
    options =>
    {
        options.MaximumBodySize = 1024;
        options.UseCaseSensitivePaths = true;
    });
builder.Services.AddHealthChecks()
    .AddCheck<CustomHealthCheck>("CustomHealthCheck",
                                 failureStatus: HealthStatus.Degraded,
                                 tags: new[] { "custom" })
    .AddSqlServer(connString)
    .AddDbContextCheck<HotelListingDbContext>();

builder.Services.AddControllers().AddOData(options =>
{
    options.Select().Filter().OrderBy();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.MapHealthChecks("/healthCustom", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
//{
//    Predicate = healthcheck => healthcheck.Tags.Contains("custom")
//    ResultStatusCodes =
//    {
//        [HealthStatus.Healthy] = StatusCodes.Status200OK,
//        [HealthStatus.Unhealthy] = StatusCodes.Status500InternalServerError,
//        [HealthStatus.Degraded] = StatusCodes.Status200OK,
//    },
//    ResponseWriter = WriteResonse
//});

app.MapHealthChecks("/health");
app.UseMiddleware<ExceptionMiddleware>();
app.UseSerilogRequestLogging();

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseResponseCaching();
app.Use(async (context, next) =>
{
    context.Response.GetTypedHeaders().CacheControl =
        new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
        {
            Public = true,
            MaxAge = TimeSpan.FromSeconds(10),
        };
    context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.Vary] = new string[] { "Accept-Encoding" };
    await next();
});


app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();


class CustomHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var isHealthy = true;

        if(isHealthy)
        {
            return Task.FromResult(HealthCheckResult.Healthy("ALl OK"));
        }
        return Task.FromResult(new HealthCheckResult(context.Registration.FailureStatus, "System unhealthy"));
    }
}