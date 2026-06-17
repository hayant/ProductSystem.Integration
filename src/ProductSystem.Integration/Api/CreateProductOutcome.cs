namespace ProductSystem.Integration.Api;

// A domain-shaped result for SyncService. Keeps HTTP status codes from leaking
// into the sync logic — if the transport changes (gRPC, message queue), only
// the client needs to translate to these outcomes.
public enum CreateProductOutcome
{
    Created,    // 201
    Duplicate,  // 409 — already exists, idempotent skip
    Failed      // anything else (validation, auth, network, server error)
}

// Aggregate result of a batch create. The same three outcomes as the single-create path, just
// counted: Duplicate folds in already-existing SKUs (idempotent skips), Failed folds in invalid
// records plus any transport/server failure of the whole batch.
public record BatchCreateOutcome(int Created, int Duplicate, int Failed);
