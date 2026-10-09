using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tsikhanau.Monolith.TestModules.Pings.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PingOptionals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "archived_on",
                schema: "pings",
                table: "pings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nickname",
                schema: "pings",
                table: "pings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "archived_on",
                schema: "pings",
                table: "pings");

            migrationBuilder.DropColumn(
                name: "nickname",
                schema: "pings",
                table: "pings");
        }
    }
}
