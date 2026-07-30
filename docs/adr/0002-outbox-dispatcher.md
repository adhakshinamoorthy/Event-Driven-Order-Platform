# ADR 0002: Separate domain events from public integration events

Status: Proposed

Domain events are persisted with the aggregate. A future dispatcher must translate them into versioned public contracts, publish with confirms, and mark outbox rows processed. Consumers must use inbox deduplication. Persisting an outbox row alone does not prove event delivery.
