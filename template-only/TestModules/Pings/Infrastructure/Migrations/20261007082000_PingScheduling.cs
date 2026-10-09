using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tsikhanau.Monolith.TestModules.Pings.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PingScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "schedule_interval",
                schema: "pings",
                table: "pings",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<int>(
                name: "schedule_repeats",
                schema: "pings",
                table: "pings",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "schedule_interval",
                schema: "pings",
                table: "pings");

            migrationBuilder.DropColumn(
                name: "schedule_repeats",
                schema: "pings",
                table: "pings");
        }
    }
}
