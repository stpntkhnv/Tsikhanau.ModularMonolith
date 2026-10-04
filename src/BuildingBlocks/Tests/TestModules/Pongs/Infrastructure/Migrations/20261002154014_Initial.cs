using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModularMonolith.TestModules.Pongs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pongs");

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "pongs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    trace_parent = table.Column<string>(type: "text", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    processed_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "internal_commands",
                schema: "pongs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enqueued_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    trace_parent = table.Column<string>(type: "text", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    processed_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_internal_commands", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "pongs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    trace_parent = table.Column<string>(type: "text", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    processed_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pongs",
                schema: "pongs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ping_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pongs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inbox_messages_next_attempt_on_occurred_on_id",
                schema: "pongs",
                table: "inbox_messages",
                columns: new[] { "next_attempt_on", "occurred_on", "id" },
                filter: "processed_on IS NULL AND failed_on IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_internal_commands_next_attempt_on_enqueued_on_id",
                schema: "pongs",
                table: "internal_commands",
                columns: new[] { "next_attempt_on", "enqueued_on", "id" },
                filter: "processed_on IS NULL AND failed_on IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_next_attempt_on_occurred_on_id",
                schema: "pongs",
                table: "outbox_messages",
                columns: new[] { "next_attempt_on", "occurred_on", "id" },
                filter: "processed_on IS NULL AND failed_on IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "pongs");

            migrationBuilder.DropTable(
                name: "internal_commands",
                schema: "pongs");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "pongs");

            migrationBuilder.DropTable(
                name: "pongs",
                schema: "pongs");
        }
    }
}
