# E-commerce microservices demo

A learning project with separate ASP.NET Core product and order APIs, EF Core SQLite databases, Redis caching and a RabbitMQ order-created queue. This is a demo, not a production commerce system.

## Run
Install Docker and Docker Compose, then `docker compose up --build`. If upgrading the previous demo, reset its SQLite volumes first with `docker compose down -v` (this deletes demo data); this sample uses EnsureCreated, not migrations. Product Swagger: http://localhost:5001/swagger; order Swagger: http://localhost:5002/swagger. RabbitMQ management: http://localhost:15672 (guest/guest, local demo only).

Create a product with `POST /products` and JSON `{"name":"Keyboard","price":120}`. List with `GET /products`; repeated reads use Redis for two minutes and creation invalidates the cache. Create an order with `POST /orders` and `{"productId":1,"quantity":2}`; its JSON event is published to the durable `orders.created` queue and consumed and logged by the product service (`docker compose logs products`). Each API has its own persistent SQLite volume. `docker compose down -v` deletes the demo databases.

## Known limitations
Order creation does not validate product availability, stock, or price. There is no authentication. The order and outbox entry are saved in one transaction. A background publisher retries pending events and waits for RabbitMQ publisher confirms. Delivery is at least once: a crash after confirmation can produce duplicates, so business consumers must deduplicate using the message ID. The consumer only logs events; there is no business processing or dead-letter handling. Redis and RabbitMQ failures can fail requests. SQLite/EnsureCreated is for the demo; use migrations and a production database for deployment. Do not expose default RabbitMQ credentials publicly.
