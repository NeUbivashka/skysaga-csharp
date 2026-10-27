using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkySaga.Data.Migrations
{
    /// <inheritdoc />
    public partial class Worlds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "map_name",
                table: "worlds",
                newName: "template");

            migrationBuilder.RenameIndex(
                name: "ix_worlds_map_name",
                table: "worlds",
                newName: "ix_worlds_template");

            migrationBuilder.AddColumn<string>(
                name: "build_access",
                table: "worlds",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Owner");

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "worlds",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Island");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_visited_at",
                table: "worlds",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "worlds",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_guild_id",
                table: "worlds",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "seed",
                table: "worlds",
                type: "integer",
                nullable: false,
                defaultValue: 1337);

            migrationBuilder.AddColumn<string>(
                name: "template_version",
                table: "worlds",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "visit_access",
                table: "worlds",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Everyone");

            migrationBuilder.AddColumn<Guid>(
                name: "current_world_id",
                table: "characters",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "map_templates",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_map_templates", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_worlds_owner_character_id_kind",
                table: "worlds",
                columns: new[] { "owner_character_id", "kind" });

            migrationBuilder.CreateIndex(
                name: "ix_worlds_owner_guild_id_kind",
                table: "worlds",
                columns: new[] { "owner_guild_id", "kind" });

            migrationBuilder.CreateIndex(
                name: "ix_worlds_template_version",
                table: "worlds",
                column: "template_version");

            migrationBuilder.CreateIndex(
                name: "ix_map_templates_name",
                table: "map_templates",
                column: "name");

            migrationBuilder.AddForeignKey(
                name: "fk_worlds_map_templates_template_version",
                table: "worlds",
                column: "template_version",
                principalTable: "map_templates",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_worlds_map_templates_template_version",
                table: "worlds");

            migrationBuilder.DropTable(
                name: "map_templates");

            migrationBuilder.DropIndex(
                name: "ix_worlds_owner_character_id_kind",
                table: "worlds");

            migrationBuilder.DropIndex(
                name: "ix_worlds_owner_guild_id_kind",
                table: "worlds");

            migrationBuilder.DropIndex(
                name: "ix_worlds_template_version",
                table: "worlds");

            migrationBuilder.DropColumn(
                name: "build_access",
                table: "worlds");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "worlds");

            migrationBuilder.DropColumn(
                name: "last_visited_at",
                table: "worlds");

            migrationBuilder.DropColumn(
                name: "name",
                table: "worlds");

            migrationBuilder.DropColumn(
                name: "owner_guild_id",
                table: "worlds");

            migrationBuilder.DropColumn(
                name: "seed",
                table: "worlds");

            migrationBuilder.DropColumn(
                name: "template_version",
                table: "worlds");

            migrationBuilder.DropColumn(
                name: "visit_access",
                table: "worlds");

            migrationBuilder.DropColumn(
                name: "current_world_id",
                table: "characters");

            migrationBuilder.RenameColumn(
                name: "template",
                table: "worlds",
                newName: "map_name");

            migrationBuilder.RenameIndex(
                name: "ix_worlds_template",
                table: "worlds",
                newName: "ix_worlds_map_name");
        }
    }
}
