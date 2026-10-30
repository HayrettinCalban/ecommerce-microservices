using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

public class OrderConsumer(IConfiguration config, ILogger<OrderConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory { HostName = config["RabbitMQ:Host"] ?? "localhost", UserName = config["RabbitMQ:User"] ?? "guest", Password = config["RabbitMQ:Password"] ?? "guest" };
                using var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();
                channel.QueueDeclare("orders.created", durable: true, exclusive: false, autoDelete: false);
                channel.BasicQos(0, 1, false);
                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += (_, e) => {
                    logger.LogInformation("Received order event: {OrderEvent}", Encoding.UTF8.GetString(e.Body.ToArray()));
                    channel.BasicAck(e.DeliveryTag, false);
                };
                channel.BasicConsume("orders.created", autoAck: false, consumer);
                while (connection.IsOpen && !stoppingToken.IsCancellationRequested) await Task.Delay(1000, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "RabbitMQ consumer disconnected; retrying"); await Task.Delay(5000, stoppingToken); }
        }
    }
}
