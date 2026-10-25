using Microsoft.EntityFrameworkCore;
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

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();

app.Run();
