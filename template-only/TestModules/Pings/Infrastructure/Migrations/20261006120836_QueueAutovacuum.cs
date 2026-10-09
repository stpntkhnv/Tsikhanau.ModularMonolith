using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tsikhanau.Monolith.TestModules.Pings.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class QueueAutovacuum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "outbox_messages",
                schema: "pings")
                .Annotation("Npgsql:StorageParameter:autovacuum_vacuum_scale_factor", 0.0)
                .Annotation("Npgsql:StorageParameter:autovacuum_vacuum_threshold", 10000);

            migrationBuilder.AlterTable(
                name: "internal_commands",
                schema: "pings")
                .Annotation("Npgsql:StorageParameter:autovacuum_vacuum_scale_factor", 0.0)
                .Annotation("Npgsql:StorageParameter:autovacuum_vacuum_threshold", 10000);

            migrationBuilder.AlterTable(
                name: "inbox_messages",
                schema: "pings")
                .Annotation("Npgsql:StorageParameter:autovacuum_vacuum_scale_factor", 0.0)
                .Annotation("Npgsql:StorageParameter:autovacuum_vacuum_threshold", 10000);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "outbox_messages",
                schema: "pings")
                .OldAnnotation("Npgsql:StorageParameter:autovacuum_vacuum_scale_factor", 0.0)
                .OldAnnotation("Npgsql:StorageParameter:autovacuum_vacuum_threshold", 10000);

            migrationBuilder.AlterTable(
                name: "internal_commands",
                schema: "pings")
                .OldAnnotation("Npgsql:StorageParameter:autovacuum_vacuum_scale_factor", 0.0)
                .OldAnnotation("Npgsql:StorageParameter:autovacuum_vacuum_threshold", 10000);

            migrationBuilder.AlterTable(
                name: "inbox_messages",
                schema: "pings")
                .OldAnnotation("Npgsql:StorageParameter:autovacuum_vacuum_scale_factor", 0.0)
                .OldAnnotation("Npgsql:StorageParameter:autovacuum_vacuum_threshold", 10000);
        }
    }
}
