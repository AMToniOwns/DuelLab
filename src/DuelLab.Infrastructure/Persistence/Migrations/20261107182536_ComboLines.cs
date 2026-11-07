using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DuelLab.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ComboLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "deck_version_combo_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeckVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Pieces = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deck_version_combo_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_deck_version_combo_lines_deck_versions_DeckVersionId",
                        column: x => x.DeckVersionId,
                        principalTable: "deck_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_deck_version_combo_lines_DeckVersionId_Position",
                table: "deck_version_combo_lines",
                columns: new[] { "DeckVersionId", "Position" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deck_version_combo_lines");
        }
    }
}
