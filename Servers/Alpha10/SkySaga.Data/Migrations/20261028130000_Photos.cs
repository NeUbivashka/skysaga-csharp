using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkySaga.Data.Migrations
{
    /// <inheritdoc />
    public partial class Photos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "photos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    upload_token = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_avatar = table.Column<bool>(type: "boolean", nullable: false),
                    capture_option = table.Column<bool>(type: "boolean", nullable: false),
                    caption = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    visible_public = table.Column<bool>(type: "boolean", nullable: false),
                    visible_friends = table.Column<bool>(type: "boolean", nullable: false),
                    likes = table.Column<int>(type: "integer", nullable: false),
                    camera_position = table.Column<float[]>(type: "real[]", nullable: false),
                    player_position = table.Column<float[]>(type: "real[]", nullable: false),
                    facing_direction = table.Column<float[]>(type: "real[]", nullable: false),
                    camera_direction = table.Column<float[]>(type: "real[]", nullable: false),
                    entity_ids = table.Column<long[]>(type: "bigint[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    md5 = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    image = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_photos", x => x.id);
                    table.ForeignKey(
                        name: "fk_photos_characters_owner_character_id",
                        column: x => x.owner_character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_photos_owner_character_id_world_id",
                table: "photos",
                columns: new[] { "owner_character_id", "world_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "photos");
        }
    }
}
