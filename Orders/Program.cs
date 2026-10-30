using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<OrderDb>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Database") ?? "Data Source=orders.db"));
builder.Services.AddSingleton(sp => new ConnectionFactory { HostName = builder.Configuration["RabbitMQ:Host"] ?? "localhost", UserName = builder.Configuration["RabbitMQ:User"] ?? "guest", Password = builder.Configuration["RabbitMQ:Password"] ?? "guest", DispatchConsumersAsync = true }.CreateConnection());
builder.Services.AddEndpointsApiExplorer(); builder.Services.AddSwaggerGen();
var app = builder.Build();
using (var scope = app.Services.CreateScope()) scope.ServiceProvider.GetRequiredService<OrderDb>().Database.EnsureCreated();
app.UseSwagger(); app.UseSwaggerUI();
app.MapPost("/orders", async (OrderInput input, OrderDb db, IConnection connection) => {
    if (input.ProductId <= 0 || input.Quantity <= 0) return Results.BadRequest("ProductId and quantity must be positive.");
    var order = new Order { ProductId = input.ProductId, Quantity = input.Quantity, CreatedAt = DateTime.UtcNow };
    db.Orders.Add(order); await db.SaveChangesAsync();
    using var channel = connection.CreateModel();
    channel.QueueDeclare("orders.created", durable:true, exclusive:false, autoDelete:false);
    var props = channel.CreateBasicProperties(); props.Persistent = true;
    channel.BasicPublish("", "orders.created", props, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(order)));
    return Results.Created($"/orders/{order.Id}", order);
});
app.MapGet("/orders/{id:int}", async (int id, OrderDb db) => await db.Orders.FindAsync(id) is Order o ? Results.Ok(o) : Results.NotFound());
app.Run();
public record OrderInput(int ProductId, int Quantity);
public class Order { public int Id {get;set;} public int ProductId {get;set;} public int Quantity {get;set;} public DateTime CreatedAt {get;set;} }
public class OrderDb(DbContextOptions<OrderDb> options) : DbContext(options) { public DbSet<Order> Orders => Set<Order>(); }
