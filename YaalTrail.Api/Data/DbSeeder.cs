using Microsoft.EntityFrameworkCore;
using YaalTrail.Api.Models;
using YaalTrail.Api.Services;

namespace YaalTrail.Api.Data;

// SRS section 6: seed the 17 proposal places (Table 1.2) and their categories.
public static class DbSeeder
{
    // FR-02: the six visitor-facing categories.
    private static readonly List<Category> Categories = new()
    {
        new Category { Name = "Religious",    Description = "Temples, churches and Buddhist viharas.",       IconRef = "religious" },
        new Category { Name = "Heritage",     Description = "Historic, archaeological and cultural sites.",  IconRef = "heritage" },
        new Category { Name = "Natural",      Description = "Beaches, wells and other natural attractions.", IconRef = "natural" },
        new Category { Name = "Recreational", Description = "Parks and leisure spaces.",                     IconRef = "recreational" },
        new Category { Name = "Dining",       Description = "Restaurants and local food stops.",             IconRef = "dining" },
        new Category { Name = "Public",       Description = "Markets and public spaces.",                    IconRef = "public" },
    };

    // One row per place. Edit values here; opening hours apply to all 7 days
    // except any day listed in ClosedOn (FR-44).
    private record PlaceSeed(
        string Name,
        string Category,
        decimal DistanceKm,
        decimal Latitude,
        decimal Longitude,
        decimal EntryFee, // LKR, 0 = free entry (FR-10)
        string Description,
        string TravelTips,
        TimeOnly Open,
        TimeOnly Close,
        DayOfWeek[]? ClosedOn = null);

    // Places open around the clock are stored as 00:00-23:59.
    private static readonly TimeOnly AllDayOpen = new(0, 0);
    private static readonly TimeOnly AllDayClose = new(23, 59);

    private static readonly List<PlaceSeed> Places = new()
    {
        new("Nallur Kandaswamy Kovil", "Religious", 1.5m,
            9.674847m, 80.029322m,
            0m,
            "The most important Hindu temple in Jaffna, dedicated to Lord Murugan and known for its golden gopuram. Its 25-day annual festival draws devotees from across the world.",
            "Men must remove shirts and all visitors must leave footwear outside. Dress modestly and avoid photography inside the inner shrine. Temple closes to visitors about 12:15-15:00.",
            new TimeOnly(4, 0), new TimeOnly(18, 15)),

        new("Old Park, Jaffna", "Recreational", 1.2m,
            9.660299m, 80.028141m,
            100m,
            "A restored colonial-era park near the Jaffna Kachcheri with shaded walkways and lawns. A quiet place to rest between sightseeing stops.",
            "Best visited in the early morning or late afternoon to avoid the midday heat.",
            new TimeOnly(9, 0), new TimeOnly(20, 0)),

        new("Mangos Restaurant", "Dining", 1.6m,
            9.659009m, 80.021995m,
            0m,
            "A popular vegetarian restaurant near Nallur serving South Indian and Jaffna-style dishes such as dosai and thali meals.",
            "Busy at lunch time and after temple poojas; arrive a little early. Combine with a visit to Nallur Kovil.",
            new TimeOnly(11, 0), new TimeOnly(23, 0)),

        new("Rio Ice Cream", "Dining", 1.8m,
            9.674013m, 80.027042m,
            0m,
            "A well-known Jaffna ice cream parlour famous for its sundaes and fruit-based desserts. A favourite stop for families after visiting Nallur.",
            "Evenings can be crowded; try a classic fruit salad with ice cream.",
            new TimeOnly(9, 0), new TimeOnly(22, 0)),

        new("Jaffna Public Library", "Heritage", 2.0m,
            9.662227m, 80.012013m,
            0m,
            "An iconic Indo-Saracenic building that was burned in 1981 and later rebuilt. It is a powerful symbol of Jaffna's love of learning and its recovery.",
            "Visitor access may be limited to certain hours and closed on public holidays. Keep quiet inside and ask before taking photographs.",
            new TimeOnly(8, 30), new TimeOnly(18, 0)),

        new("Jaffna Central Market", "Public", 2.0m,
            9.667658m, 80.009895m,
            0m,
            "A busy market in the town centre selling fresh produce, Jaffna mangoes, palmyra products and dried fish. A good place to see daily local life.",
            "Go in the morning for the freshest produce. Watch your belongings in crowded lanes and carry small notes.",
            new TimeOnly(8, 0), new TimeOnly(21, 0)),

        new("Jaffna Cultural Centre", "Heritage", 2.0m,
            9.663102m, 80.012319m,
            0m, // VERIFY entry fee
            "A modern cultural complex with an auditorium, exhibition spaces and a public square, built to host performances and cultural events.",
            "Check for scheduled events before visiting; some halls are open only during programmes.",
            new TimeOnly(9, 0), new TimeOnly(17, 0)), // VERIFY opening hours

        new("Sangiliyan Thoppu & Mantri Manai", "Heritage", 2.2m,
            9.677674m, 80.035971m,
            0m,
            "The remains of the royal grounds of the Jaffna Kingdom, including the arched gateway of Sangiliyan Thoppu and the nearby Mantri Manai (minister's residence).",
            "The structures are fragile; do not climb on them. Little shade is available, so carry water.",
            AllDayOpen, AllDayClose),

        new("St. Mary's Cathedral", "Religious", 2.3m,
            9.656318m, 80.022624m,
            0m,
            "A large Catholic cathedral in Jaffna town with a long history dating back to the colonial period. It remains the centre of the local Catholic community.",
            "Dress modestly and stay quiet during services, especially on Sunday mornings.",
            new TimeOnly(6, 0), new TimeOnly(19, 0)),

        new("Jaffna Fort", "Heritage", 2.5m,
            9.662242m, 80.008425m,
            60m,
            "A star-shaped fort first built by the Portuguese and later expanded by the Dutch. Walk the ramparts for views over the Jaffna lagoon.",
            "Very hot in the afternoon; visit early or near sunset. Wear comfortable shoes for walking on the ramparts. Army-garrisoned, access may be limited; check before visiting.",
            new TimeOnly(7, 0), new TimeOnly(18, 0)),

        new("Kandarodai (Kadurugoda) Vihara", "Heritage", 11m,
            9.747033m, 80.006631m,
            0m,
            "An archaeological site with dozens of small, bell-shaped stupas from the early Buddhist period. It is one of the most unusual sites in the peninsula.",
            "Remove footwear near the stupas. There is little shade, so bring a hat and water.",
            new TimeOnly(6, 0), new TimeOnly(20, 0)),

        new("Nilavarai Bottomless Well, Puttur", "Natural", 12m,
            9.754665m, 80.088229m,
            0m,
            "A natural limestone well filled with fresh water, locally believed to be bottomless. It is linked to legends from the Ramayana.",
            "Swimming is not allowed. Keep children away from the edge.",
            AllDayOpen, AllDayClose),

        new("Maviddapuram Kandaswamy Kovil", "Religious", 16m,
            9.801110m, 80.035060m, // VERIFY on Google Maps
            0m,
            "An ancient Murugan temple in the north of the peninsula with a tall gopuram, closely connected to the Keerimalai area.",
            "Men must remove shirts before entering. Can be combined with Keerimalai on the same trip.",
            new TimeOnly(6, 0), new TimeOnly(18, 0)), // VERIFY opening hours

        new("Keerimalai Naguleswaram Temple", "Religious", 18m,
            9.813603m, 80.012062m,
            0m,
            "One of the ancient Shiva temples of Sri Lanka, located next to the Keerimalai freshwater springs by the sea.",
            "The sacred springs beside the temple have separate bathing areas for men and women. Bring a change of clothes if you plan to bathe. Temple closes to visitors about 13:00-16:00.",
            new TimeOnly(6, 0), new TimeOnly(19, 0)),

        new("Casuarina Beach, Karainagar", "Natural", 20m,
            9.762455m, 79.885095m,
            0m,
            "A long, shallow beach lined with casuarina trees on Karainagar island, reached by a causeway. A popular spot for families at weekends.",
            "Reached by causeway; check the road before travelling during heavy rain. Facilities are limited, so bring water and snacks. Parking Rs 100, shower Rs 100.",
            new TimeOnly(4, 0), new TimeOnly(18, 0)),

        new("Charty Beach, Vellanai", "Natural", 20m,
            9.632785m, 79.920285m,
            0m,
            "A calm beach on Velanai island with shallow water and sunset views across the sea.",
            "Reached over the Velanai causeway. Swim only in calm, shallow areas and avoid the water when it is rough.",
            new TimeOnly(4, 0), new TimeOnly(18, 0)),

        new("Dambakola Patuna Vihara", "Religious", 23m,
            9.786702m, 79.943564m,
            0m,
            "A Buddhist temple at the ancient port of Jambukola, believed to be where Sanghamitta Theri landed with the sapling of the sacred Bo tree.",
            "The road passes through a restricted coastal area; carry identification. Dress modestly and remove footwear at the shrine.",
            new TimeOnly(9, 0), new TimeOnly(18, 0)),
    };

    private const string AdminEmail = "admin@yaaltrail.local";

    // Adds the categories and places. Call only when the Categories table is empty.
    public static async Task SeedPlacesAsync(AppDbContext db)
    {
        DateTime now = DateTime.UtcNow;

        db.Categories.AddRange(Categories);

        var categoryByName = Categories.ToDictionary(c => c.Name);

        foreach (var seed in Places)
        {
            var place = new Place
            {
                Category = categoryByName[seed.Category],
                Name = seed.Name,
                Description = seed.Description,
                Latitude = seed.Latitude,
                Longitude = seed.Longitude,
                DistanceKm = seed.DistanceKm,
                EntryFee = seed.EntryFee,
                TravelTips = seed.TravelTips,
                IsPublished = true,
                CreatedAt = now,
                UpdatedAt = now,
            };

            // One opening-hour row per day of the week (SRS section 6).
            foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
            {
                bool closed = seed.ClosedOn?.Contains(day) ?? false;
                place.OpeningHours.Add(new OpeningHour
                {
                    DayOfWeek = day,
                    OpenTime = closed ? null : seed.Open,
                    CloseTime = closed ? null : seed.Close,
                    IsClosedAllDay = closed,
                });
            }

            db.Places.Add(place);
        }

        await db.SaveChangesAsync();
    }

    // Adds the initial administrator if it does not exist yet.
    public static async Task SeedAdminAsync(AppDbContext db, IConfiguration config, IPasswordHasher hasher)
    {
        if (await db.AdminUsers.AnyAsync(a => a.Email == AdminEmail))
            return;

        // NFR-15: the initial admin password comes only from user-secrets.
        string? initialPassword = config["Admin:InitialPassword"];
        if (string.IsNullOrWhiteSpace(initialPassword))
            return;

        db.AdminUsers.Add(new AdminUser
        {
            Email = AdminEmail,
            DisplayName = "Administrator",
            PasswordHash = hasher.Hash(initialPassword), // FR-50, NFR-12
        });

        await db.SaveChangesAsync();
    }
}
