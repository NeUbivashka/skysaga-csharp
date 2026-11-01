using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkySaga.Data.Migrations
{
    /// <inheritdoc />
    public partial class OreVeins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "fk_keystone_veins_worlds_world_id", table: "keystone_veins");
            migrationBuilder.DropPrimaryKey(name: "pk_keystone_veins", table: "keystone_veins");
            migrationBuilder.RenameTable(name: "keystone_veins", newName: "ore_veins");
            migrationBuilder.AddColumn<short>(name: "kind", table: "ore_veins", type: "smallint", nullable: false, defaultValue: (short)0);
            migrationBuilder.AddPrimaryKey(name: "pk_ore_veins", table: "ore_veins", columns: new[] { "world_id", "x", "y", "z" });
            migrationBuilder.AddForeignKey(
                name: "fk_ore_veins_worlds_world_id",
                table: "ore_veins",
                column: "world_id",
                principalTable: "worlds",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "fk_ore_veins_worlds_world_id", table: "ore_veins");
            migrationBuilder.DropPrimaryKey(name: "pk_ore_veins", table: "ore_veins");
            migrationBuilder.DropColumn(name: "kind", table: "ore_veins");
            migrationBuilder.RenameTable(name: "ore_veins", newName: "keystone_veins");
            migrationBuilder.AddPrimaryKey(name: "pk_keystone_veins", table: "keystone_veins", columns: new[] { "world_id", "x", "y", "z" });
            migrationBuilder.AddForeignKey(
                name: "fk_keystone_veins_worlds_world_id",
                table: "keystone_veins",
                column: "world_id",
                principalTable: "worlds",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
