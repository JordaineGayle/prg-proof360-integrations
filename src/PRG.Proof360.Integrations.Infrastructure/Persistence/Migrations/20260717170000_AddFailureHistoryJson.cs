using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRG.Proof360.Integrations.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
[DbContext(typeof(ConnectorDbContext))]
[Migration("20260717170000_AddFailureHistoryJson")]
public partial class AddFailureHistoryJson : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "FailureHistoryJson",
            table: "inbox_messages",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "FailureHistoryJson",
            table: "outbox_messages",
            type: "TEXT",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "FailureHistoryJson",
            table: "inbox_messages");

        migrationBuilder.DropColumn(
            name: "FailureHistoryJson",
            table: "outbox_messages");
    }
}
