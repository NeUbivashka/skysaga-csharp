using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkySaga.Data.Migrations
{
    /// <inheritdoc />
    public partial class Premium : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "premium_until",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "premium_until",
                table: "accounts");
        }
    }
}
