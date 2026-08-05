# Verification

CI proves restore, formatting, Release compilation, four deterministic tests, NuGet vulnerability audit, and Compose model validity. It does not prove SQL Server persistence or RabbitMQ delivery. The current development startup creates a schema with `EnsureCreated`; production migrations and an outbox dispatcher are not yet implemented.
