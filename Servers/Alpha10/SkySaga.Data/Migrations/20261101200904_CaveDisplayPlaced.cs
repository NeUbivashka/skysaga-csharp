using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkySaga.Data.Migrations
{
    /// <inheritdoc />
    public partial class CaveDisplayPlaced : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "cave_display_placed",
                table: "worlds",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cave_display_placed",
                table: "worlds");
        }
    }
}
