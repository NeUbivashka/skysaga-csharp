using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SkySaga.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    banned_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ban_reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "game_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    character_id = table.Column<Guid>(type: "uuid", nullable: true),
                    world_id = table.Column<Guid>(type: "uuid", nullable: true),
                    data = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "worlds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    map_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    owner_character_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_worlds", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "characters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_customised = table.Column<bool>(type: "boolean", nullable: false),
                    customisation_data = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    position_map = table.Column<string>(type: "text", nullable: true),
                    position_x = table.Column<float>(type: "real", nullable: false),
                    position_y = table.Column<float>(type: "real", nullable: false),
                    position_z = table.Column<float>(type: "real", nullable: false),
                    yaw_degrees = table.Column<float>(type: "real", nullable: false),
                    look_pitch = table.Column<float>(type: "real", nullable: false),
                    initial_hp = table.Column<int>(type: "integer", nullable: false),
                    whole_hearts = table.Column<int>(type: "integer", nullable: false),
                    half_hearts = table.Column<int>(type: "integer", nullable: false),
                    challenge_refresh_index = table.Column<long>(type: "bigint", nullable: true),
                    available_emotes = table.Column<List<string>>(type: "text[]", nullable: true),
                    keybound_emotes = table.Column<List<string>>(type: "text[]", nullable: true),
                    known_recipes = table.Column<List<string>>(type: "text[]", nullable: true),
                    completed_challenges = table.Column<List<string>>(type: "text[]", nullable: true),
                    settings = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_characters", x => x.id);
                    table.ForeignKey(
                        name: "fk_characters_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "checkpoints",
                columns: table => new
                {
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    x = table.Column<float>(type: "real", nullable: false),
                    y = table.Column<float>(type: "real", nullable: false),
                    z = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_checkpoints", x => new { x.world_id, x.character_id });
                    table.ForeignKey(
                        name: "fk_checkpoints_worlds_world_id",
                        column: x => x.world_id,
                        principalTable: "worlds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "placed_devices",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_crc = table.Column<long>(type: "bigint", nullable: false),
                    x = table.Column<int>(type: "integer", nullable: false),
                    y = table.Column<int>(type: "integer", nullable: false),
                    z = table.Column<int>(type: "integer", nullable: false),
                    owner_character_id = table.Column<Guid>(type: "uuid", nullable: true),
                    materials = table.Column<long?[]>(type: "bigint[]", nullable: false),
                    items = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_placed_devices", x => x.id);
                    table.ForeignKey(
                        name: "fk_placed_devices_worlds_world_id",
                        column: x => x.world_id,
                        principalTable: "worlds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "voxel_edits",
                columns: table => new
                {
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    x = table.Column<int>(type: "integer", nullable: false),
                    y = table.Column<int>(type: "integer", nullable: false),
                    z = table.Column<int>(type: "integer", nullable: false),
                    block = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_voxel_edits", x => new { x.world_id, x.x, x.y, x.z });
                    table.ForeignKey(
                        name: "fk_voxel_edits_worlds_world_id",
                        column: x => x.world_id,
                        principalTable: "worlds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "active_challenges",
                columns: table => new
                {
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    progress = table.Column<long>(type: "bigint", nullable: false),
                    todo_timestamp = table.Column<decimal>(type: "numeric(20,0)", precision: 20, scale: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_active_challenges", x => new { x.character_id, x.name });
                    table.ForeignKey(
                        name: "fk_active_challenges_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_currencies",
                columns: table => new
                {
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_hash = table.Column<long>(type: "bigint", nullable: false),
                    value = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_character_currencies", x => new { x.character_id, x.name_hash });
                    table.ForeignKey(
                        name: "fk_character_currencies_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_jobs",
                columns: table => new
                {
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    rank = table.Column<short>(type: "smallint", nullable: false),
                    experience = table.Column<long>(type: "bigint", nullable: false),
                    prestige_rank = table.Column<long>(type: "bigint", nullable: false),
                    available = table.Column<List<string>>(type: "text[]", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_character_jobs", x => new { x.character_id, x.name });
                    table.ForeignKey(
                        name: "fk_character_jobs_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inventory_items",
                columns: table => new
                {
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot = table.Column<int>(type: "integer", nullable: false),
                    name_hash = table.Column<long>(type: "bigint", nullable: false),
                    count = table.Column<int>(type: "integer", nullable: false),
                    item_uuid = table.Column<string>(type: "text", nullable: true),
                    materials = table.Column<long?[]>(type: "bigint[]", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_items", x => new { x.character_id, x.slot });
                    table.ForeignKey(
                        name: "fk_inventory_items_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_username",
                table: "accounts",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_characters_account_id",
                table: "characters",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_characters_name",
                table: "characters",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_game_events_character_id_time",
                table: "game_events",
                columns: new[] { "character_id", "time" });

            migrationBuilder.CreateIndex(
                name: "ix_game_events_time",
                table: "game_events",
                column: "time");

            migrationBuilder.CreateIndex(
                name: "ix_placed_devices_world_id",
                table: "placed_devices",
                column: "world_id");

            migrationBuilder.CreateIndex(
                name: "ix_worlds_map_name",
                table: "worlds",
                column: "map_name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "active_challenges");

            migrationBuilder.DropTable(
                name: "character_currencies");

            migrationBuilder.DropTable(
                name: "character_jobs");

            migrationBuilder.DropTable(
                name: "checkpoints");

            migrationBuilder.DropTable(
                name: "game_events");

            migrationBuilder.DropTable(
                name: "inventory_items");

            migrationBuilder.DropTable(
                name: "placed_devices");

            migrationBuilder.DropTable(
                name: "voxel_edits");

            migrationBuilder.DropTable(
                name: "characters");

            migrationBuilder.DropTable(
                name: "worlds");

            migrationBuilder.DropTable(
                name: "accounts");
        }
    }
}
