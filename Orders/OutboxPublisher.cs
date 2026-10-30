using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using System.Text;

public class OutboxPublisher(IServiceScopeFactory scopes, IConfiguration config, ILogger<OutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<OrderDb>();
                var pending = await db.Outbox.Where(x => x.PublishedAt == null).OrderBy(x => x.Id).Take(50).ToListAsync(stoppingToken);
                if (pending.Count > 0)
                {
                    var factory = new ConnectionFactory { HostName = config["RabbitMQ:Host"] ?? "localhost", UserName = config["RabbitMQ:User"] ?? "guest", Password = config["RabbitMQ:Password"] ?? "guest" };
                    using var connection = factory.CreateConnection();
                    using var channel = connection.CreateModel();
                    channel.QueueDeclare("orders.created", durable: true, exclusive: false, autoDelete: false);
                    channel.ConfirmSelect();
                    foreach (var entry in pending)
                    {
                        var props = channel.CreateBasicProperties(); props.Persistent = true; props.MessageId = entry.Id.ToString();
                        channel.BasicPublish("", "orders.created", true, props, Encoding.UTF8.GetBytes(entry.Payload));
                        channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));
                        entry.PublishedAt = DateTime.UtcNow;
                        await db.SaveChangesAsync(stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "Outbox delivery failed; will retry"); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
