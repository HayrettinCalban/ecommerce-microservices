using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<ProductDb>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Database") ?? "Data Source=products.db"));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(builder.Configuration["Redis"] ?? "localhost:6379"));
builder.Services.AddHostedService<OrderConsumer>();
builder.Services.AddEndpointsApiExplorer(); builder.Services.AddSwaggerGen();
var app = builder.Build();
using (var scope = app.Services.CreateScope()) scope.ServiceProvider.GetRequiredService<ProductDb>().Database.EnsureCreated();
app.UseSwagger(); app.UseSwaggerUI();
app.MapGet("/products", async (ProductDb db, IConnectionMultiplexer redis) => {
    var cache = redis.GetDatabase();
    var cached = await cache.StringGetAsync("products:all");
    if (cached.HasValue) return Results.Content(cached.ToString(), "application/json");
    var products = await db.Products.AsNoTracking().ToListAsync();
    var json = JsonSerializer.Serialize(products);
    await cache.StringSetAsync("products:all", json, TimeSpan.FromMinutes(2));
    return Results.Content(json, "application/json");
});
app.MapPost("/products", async (ProductInput input, ProductDb db, IConnectionMultiplexer redis) => {
    if (string.IsNullOrWhiteSpace(input.Name) || input.Price <= 0) return Results.BadRequest("Name and a positive price are required.");
    var product = new Product { Name = input.Name.Trim(), Price = input.Price };
    db.Products.Add(product); await db.SaveChangesAsync();
    await redis.GetDatabase().KeyDeleteAsync("products:all");
    return Results.Created($"/products/{product.Id}", product);
});
app.MapGet("/products/{id:int}", async (int id, ProductDb db) => await db.Products.FindAsync(id) is Product p ? Results.Ok(p) : Results.NotFound());
app.Run();
public record ProductInput(string Name, decimal Price);
public class Product { public int Id {get;set;} public string Name {get;set;} = ""; public decimal Price {get;set;} }
public class ProductDb(DbContextOptions<ProductDb> options) : DbContext(options) { public DbSet<Product> Products => Set<Product>(); }
