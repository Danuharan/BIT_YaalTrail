using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using YaalTrail.Api.Configuration;
using YaalTrail.Api.Data;
using YaalTrail.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Development only: count SQL queries per request (see the logging middleware below).
builder.Services.AddScoped<SqlQueryCounter>();

builder.Services.AddDbContext<AppDbContext>((services, options) =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"));

    if (builder.Environment.IsDevelopment())
        options.AddInterceptors(new SqlQueryCountingInterceptor(services.GetRequiredService<SqlQueryCounter>()));
});

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ICatalogueCache, CatalogueCache>();

builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IPlaceService, PlaceService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();

// SRS 4.4 day planner: pure calculation services (NFR-23) plus the database step.
builder.Services.AddSingleton<IDistanceService, DistanceService>();
builder.Services.AddSingleton<IOpeningHoursValidator, OpeningHoursValidator>();
builder.Services.AddSingleton<IPlanBuilderService, PlanBuilderService>();
builder.Services.AddScoped<IPlanService, PlanService>();

// FR-15: Chundikuli, the 25 km radius and the average speed (SRS 6.4).
builder.Services.Configure<PlannerOptions>(
    builder.Configuration.GetSection(PlannerOptions.SectionName));

// SRS 4.6, 4.7: administrator module.
builder.Services.AddSingleton<IAdminSessionStore, AdminSessionStore>();
builder.Services.AddScoped<IAdminAuthService, AdminAuthService>();
builder.Services.AddScoped<IAdminPlaceService, AdminPlaceService>();
builder.Services.AddScoped<IAdminCategoryService, AdminCategoryService>();

// FR-51, NFR-15: the signing key comes only from user-secrets / environment ("Jwt:Key").
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (Encoding.UTF8.GetByteCount(jwtOptions.Key) < JwtOptions.MinKeyBytes)
{
    throw new InvalidOperationException(
        $"Jwt:Key is missing or shorter than {JwtOptions.MinKeyBytes} bytes. " +
        "Set it with: dotnet user-secrets set \"Jwt:Key\" \"<random value>\"");
}

// FR-49, FR-52, BR-02: admin endpoints accept only a valid, unexpired, correctly signed token.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep "sub", "jti" and "role" as they are in the token
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero, // FR-51: no extra minutes after the 60-minute expiry
            RequireExpirationTime = true,
            NameClaimType = "sub",
            RoleClaimType = "role",
        };

        options.Events = new JwtBearerEvents
        {
            // FR-55, FR-56: the token must also belong to a session that is still active
            // (not signed out, not idle for 30 minutes). Otherwise the request gets 401.
            OnTokenValidated = context =>
            {
                string? jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                var sessions = context.HttpContext.RequestServices.GetRequiredService<IAdminSessionStore>();

                if (jti == null || !sessions.TryTouch(jti))
                    context.Fail("Session has ended.");

                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Lets the admin endpoints be tried in Swagger UI with "Authorize" + the token.
    var bearer = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
    };
    options.AddSecurityDefinition("Bearer", bearer);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [bearer] = Array.Empty<string>() });
});

var app = builder.Build();

// Seed initial data: places only when there are no categories yet,
// the admin user only when it does not exist yet.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!await db.Categories.AnyAsync())
    {
        await DbSeeder.SeedPlacesAsync(db);
    }

    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    await DbSeeder.SeedAdminAsync(db, app.Configuration, hasher);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Log how many SQL queries each API request needed (NFR-02 check).
    app.Use(async (context, next) =>
    {
        await next();

        if (context.Request.Path.StartsWithSegments("/api"))
        {
            var counter = context.RequestServices.GetRequiredService<SqlQueryCounter>();
            app.Logger.LogInformation("{Method} {Path}{Query} -> {Status}: {Count} SQL queries",
                context.Request.Method, context.Request.Path, context.Request.QueryString,
                context.Response.StatusCode, counter.Count);
        }
    });
}

// NFR-11: HTTPS only (Kestrel uses TLS 1.2 or higher by default).
if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
