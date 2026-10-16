using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tsikhanau.Monolith.TestModules.Pings.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class QueueSecretsAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "contains_secrets",
                schema: "pings",
                table: "outbox_messages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "contains_secrets",
                schema: "pings",
                table: "internal_commands",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "contains_secrets",
                schema: "pings",
                table: "inbox_messages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_failed_on",
                schema: "pings",
                table: "outbox_messages",
                column: "failed_on",
                filter: "failed_on IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_on",
                schema: "pings",
                table: "outbox_messages",
                column: "processed_on",
                filter: "processed_on IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_internal_commands_failed_on",
                schema: "pings",
                table: "internal_commands",
                column: "failed_on",
                filter: "failed_on IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_internal_commands_processed_on",
                schema: "pings",
                table: "internal_commands",
                column: "processed_on",
                filter: "processed_on IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_inbox_messages_failed_on",
                schema: "pings",
                table: "inbox_messages",
                column: "failed_on",
                filter: "failed_on IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_inbox_messages_processed_on",
                schema: "pings",
                table: "inbox_messages",
                column: "processed_on",
                filter: "processed_on IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_failed_on",
                schema: "pings",
                table: "outbox_messages");

            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_processed_on",
                schema: "pings",
                table: "outbox_messages");

            migrationBuilder.DropIndex(
                name: "ix_internal_commands_failed_on",
                schema: "pings",
                table: "internal_commands");

            migrationBuilder.DropIndex(
                name: "ix_internal_commands_processed_on",
                schema: "pings",
                table: "internal_commands");

            migrationBuilder.DropIndex(
                name: "ix_inbox_messages_failed_on",
                schema: "pings",
                table: "inbox_messages");

            migrationBuilder.DropIndex(
                name: "ix_inbox_messages_processed_on",
                schema: "pings",
                table: "inbox_messages");

            migrationBuilder.DropColumn(
                name: "contains_secrets",
                schema: "pings",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "contains_secrets",
                schema: "pings",
                table: "internal_commands");

            migrationBuilder.DropColumn(
                name: "contains_secrets",
                schema: "pings",
                table: "inbox_messages");
        }
    }
}
