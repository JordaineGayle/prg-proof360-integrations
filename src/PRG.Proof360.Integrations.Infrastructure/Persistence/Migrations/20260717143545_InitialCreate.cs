using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRG.Proof360.Integrations.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "audit_events",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                CorrelationId = table.Column<string>(type: "TEXT", nullable: true),
                CausationId = table.Column<string>(type: "TEXT", nullable: true),
                Direction = table.Column<string>(type: "TEXT", nullable: false),
                ProviderName = table.Column<string>(type: "TEXT", nullable: false),
                ProviderInstanceId = table.Column<string>(type: "TEXT", nullable: true),
                Operation = table.Column<string>(type: "TEXT", nullable: false),
                CanonicalEntityType = table.Column<string>(type: "TEXT", nullable: true),
                CanonicalId = table.Column<Guid>(type: "TEXT", nullable: true),
                EventId = table.Column<string>(type: "TEXT", nullable: true),
                Attempt = table.Column<int>(type: "INTEGER", nullable: false),
                Result = table.Column<string>(type: "TEXT", nullable: false),
                ErrorCategory = table.Column<string>(type: "TEXT", nullable: true),
                LatencyMilliseconds = table.Column<long>(type: "INTEGER", nullable: true),
                SchemaVersion = table.Column<string>(type: "TEXT", nullable: true),
                PayloadHash = table.Column<string>(type: "TEXT", nullable: true),
                Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_audit_events", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "connector_states",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ProviderName = table.Column<string>(type: "TEXT", nullable: false),
                ProviderInstanceId = table.Column<string>(type: "TEXT", nullable: false),
                LastSuccessfulSyncAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                LastCheckpoint = table.Column<string>(type: "TEXT", nullable: true),
                CircuitState = table.Column<string>(type: "TEXT", nullable: false),
                InboxBacklogCount = table.Column<int>(type: "INTEGER", nullable: false),
                OutboxBacklogCount = table.Column<int>(type: "INTEGER", nullable: false),
                DeadLetterCount = table.Column<int>(type: "INTEGER", nullable: false),
                LastErrorCategory = table.Column<string>(type: "TEXT", nullable: true),
                LastErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_connector_states", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "inbox_messages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ProviderName = table.Column<string>(type: "TEXT", nullable: false),
                ProviderInstanceId = table.Column<string>(type: "TEXT", nullable: false),
                EventId = table.Column<string>(type: "TEXT", nullable: false),
                EventType = table.Column<string>(type: "TEXT", nullable: false),
                SchemaVersion = table.Column<string>(type: "TEXT", nullable: true),
                EventVersion = table.Column<long>(type: "INTEGER", nullable: true),
                CorrelationId = table.Column<string>(type: "TEXT", nullable: true),
                CausationId = table.Column<string>(type: "TEXT", nullable: true),
                OccurredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                ReceivedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                PayloadEnvelope = table.Column<string>(type: "TEXT", nullable: false),
                PayloadHash = table.Column<string>(type: "TEXT", nullable: false),
                State = table.Column<string>(type: "TEXT", nullable: false),
                AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                NextAttemptAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                ErrorCategory = table.Column<string>(type: "TEXT", nullable: true),
                ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                RowVersion = table.Column<uint>(type: "INTEGER", nullable: false, defaultValue: 0u)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_inbox_messages", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "jobs",
            columns: table => new
            {
                job_id = table.Column<Guid>(type: "TEXT", nullable: false),
                source = table.Column<string>(type: "TEXT", nullable: false),
                transcript_id = table.Column<Guid>(type: "TEXT", nullable: true),
                customer_name = table.Column<string>(type: "TEXT", nullable: true),
                customer_phone = table.Column<string>(type: "TEXT", nullable: true),
                customer_email = table.Column<string>(type: "TEXT", nullable: true),
                address_street = table.Column<string>(type: "TEXT", nullable: true),
                address_unit = table.Column<string>(type: "TEXT", nullable: true),
                address_city = table.Column<string>(type: "TEXT", nullable: true),
                address_postal = table.Column<string>(type: "TEXT", nullable: true),
                service_type = table.Column<string>(type: "TEXT", nullable: true),
                subcategory = table.Column<string>(type: "TEXT", nullable: true),
                priority = table.Column<string>(type: "TEXT", nullable: true),
                window_start = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                window_end = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                notes_scope = table.Column<string>(type: "TEXT", nullable: true),
                compliance_only = table.Column<bool>(type: "INTEGER", nullable: false),
                status = table.Column<string>(type: "TEXT", nullable: false),
                assigned_vendor_id = table.Column<Guid>(type: "TEXT", nullable: true),
                ai_confidence = table.Column<decimal>(type: "TEXT", precision: 5, scale: 4, nullable: true),
                ai_json = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_jobs", x => x.job_id);
            });

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ProviderName = table.Column<string>(type: "TEXT", nullable: false),
                ProviderInstanceId = table.Column<string>(type: "TEXT", nullable: false),
                OperationType = table.Column<string>(type: "TEXT", nullable: false),
                IdempotencyKey = table.Column<string>(type: "TEXT", nullable: false),
                CanonicalEntityType = table.Column<string>(type: "TEXT", nullable: false),
                CanonicalId = table.Column<Guid>(type: "TEXT", nullable: false),
                ExpectedCanonicalVersion = table.Column<long>(type: "INTEGER", nullable: true),
                CommandPayload = table.Column<string>(type: "TEXT", nullable: false),
                State = table.Column<string>(type: "TEXT", nullable: false),
                AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                NextAttemptAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                ResultReference = table.Column<string>(type: "TEXT", nullable: true),
                ErrorCategory = table.Column<string>(type: "TEXT", nullable: true),
                ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                RowVersion = table.Column<uint>(type: "INTEGER", nullable: false, defaultValue: 0u)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_outbox_messages", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "provider_identity_links",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ProviderName = table.Column<string>(type: "TEXT", nullable: false),
                ProviderInstanceId = table.Column<string>(type: "TEXT", nullable: false),
                ExternalEntityType = table.Column<string>(type: "TEXT", nullable: false),
                ExternalId = table.Column<string>(type: "TEXT", nullable: false),
                CanonicalEntityType = table.Column<string>(type: "TEXT", nullable: false),
                CanonicalId = table.Column<Guid>(type: "TEXT", nullable: false),
                MatchKey = table.Column<string>(type: "TEXT", nullable: true),
                LastAppliedVersion = table.Column<long>(type: "INTEGER", nullable: true),
                LastAppliedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                PayloadHash = table.Column<string>(type: "TEXT", nullable: true),
                RowVersion = table.Column<uint>(type: "INTEGER", nullable: false, defaultValue: 0u)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_provider_identity_links", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "transcripts",
            columns: table => new
            {
                transcript_id = table.Column<Guid>(type: "TEXT", nullable: false),
                vendor_ref = table.Column<Guid>(type: "TEXT", nullable: true),
                job_ref = table.Column<Guid>(type: "TEXT", nullable: true),
                direction = table.Column<string>(type: "TEXT", nullable: true),
                agent_name = table.Column<string>(type: "TEXT", nullable: true),
                contact_phone = table.Column<string>(type: "TEXT", nullable: true),
                contact_email = table.Column<string>(type: "TEXT", nullable: true),
                call_start = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                call_end = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                duration = table.Column<int>(type: "INTEGER", nullable: true),
                summary = table.Column<string>(type: "TEXT", nullable: true),
                topics = table.Column<string>(type: "TEXT", nullable: true),
                sentiment = table.Column<string>(type: "TEXT", nullable: true),
                synced_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                Raw_text = table.Column<string>(type: "TEXT", nullable: true),
                City = table.Column<string>(type: "TEXT", nullable: true),
                status = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_transcripts", x => x.transcript_id);
            });

        migrationBuilder.CreateTable(
            name: "vendors",
            columns: table => new
            {
                vendor_id = table.Column<Guid>(type: "TEXT", nullable: false),
                compliance_id = table.Column<string>(type: "TEXT", nullable: true),
                license_number = table.Column<string>(type: "TEXT", nullable: true),
                license_expiry = table.Column<DateOnly>(type: "TEXT", nullable: true),
                insurance_policy = table.Column<string>(type: "TEXT", nullable: true),
                insurance_expiry = table.Column<DateOnly>(type: "TEXT", nullable: true),
                insurance_coverage = table.Column<string>(type: "TEXT", nullable: true),
                wcb_number = table.Column<string>(type: "TEXT", nullable: true),
                status = table.Column<string>(type: "TEXT", nullable: false),
                ai_confidence = table.Column<decimal>(type: "TEXT", precision: 5, scale: 4, nullable: true),
                missing_items = table.Column<string>(type: "TEXT", nullable: true),
                rationale = table.Column<string>(type: "TEXT", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_vendors", x => x.vendor_id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_audit_events_CorrelationId",
            table: "audit_events",
            column: "CorrelationId");

        migrationBuilder.CreateIndex(
            name: "IX_audit_events_Timestamp",
            table: "audit_events",
            column: "Timestamp");

        migrationBuilder.CreateIndex(
            name: "ux_connector_state_instance",
            table: "connector_states",
            columns: new[] { "ProviderName", "ProviderInstanceId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_inbox_messages_State_NextAttemptAt",
            table: "inbox_messages",
            columns: new[] { "State", "NextAttemptAt" });

        migrationBuilder.CreateIndex(
            name: "ux_inbox_event",
            table: "inbox_messages",
            columns: new[] { "ProviderInstanceId", "EventId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_jobs_assigned_vendor_id",
            table: "jobs",
            column: "assigned_vendor_id");

        migrationBuilder.CreateIndex(
            name: "IX_jobs_status",
            table: "jobs",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_State_NextAttemptAt",
            table: "outbox_messages",
            columns: new[] { "State", "NextAttemptAt" });

        migrationBuilder.CreateIndex(
            name: "ux_outbox_idempotency",
            table: "outbox_messages",
            columns: new[] { "ProviderInstanceId", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_identity_canonical",
            table: "provider_identity_links",
            columns: new[] { "ProviderInstanceId", "CanonicalEntityType", "CanonicalId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_identity_external",
            table: "provider_identity_links",
            columns: new[] { "ProviderInstanceId", "ExternalEntityType", "ExternalId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "audit_events");

        migrationBuilder.DropTable(
            name: "connector_states");

        migrationBuilder.DropTable(
            name: "inbox_messages");

        migrationBuilder.DropTable(
            name: "jobs");

        migrationBuilder.DropTable(
            name: "outbox_messages");

        migrationBuilder.DropTable(
            name: "provider_identity_links");

        migrationBuilder.DropTable(
            name: "transcripts");

        migrationBuilder.DropTable(
            name: "vendors");
    }
}
