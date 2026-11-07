using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DuelLab.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BanlistSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BanlistId",
                table: "decks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "banlists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProviderVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ProviderUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_banlists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "banlist_entries",
                columns: table => new
                {
                    BanlistId = table.Column<Guid>(type: "uuid", nullable: false),
                    CardId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_banlist_entries", x => new { x.BanlistId, x.CardId });
                    table.ForeignKey(
                        name: "FK_banlist_entries_banlists_BanlistId",
                        column: x => x.BanlistId,
                        principalTable: "banlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_banlist_entries_cards_CardId",
                        column: x => x.CardId,
                        principalTable: "cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_decks_BanlistId",
                table: "decks",
                column: "BanlistId");

            migrationBuilder.CreateIndex(
                name: "IX_banlist_entries_CardId",
                table: "banlist_entries",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_banlists_FirstSeenAt",
                table: "banlists",
                column: "FirstSeenAt");

            migrationBuilder.AddForeignKey(
                name: "FK_decks_banlists_BanlistId",
                table: "decks",
                column: "BanlistId",
                principalTable: "banlists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_decks_banlists_BanlistId",
                table: "decks");

            migrationBuilder.DropTable(
                name: "banlist_entries");

            migrationBuilder.DropTable(
                name: "banlists");

            migrationBuilder.DropIndex(
                name: "IX_decks_BanlistId",
                table: "decks");

            migrationBuilder.DropColumn(
                name: "BanlistId",
                table: "decks");
        }
    }
}
