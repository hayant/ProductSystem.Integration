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
