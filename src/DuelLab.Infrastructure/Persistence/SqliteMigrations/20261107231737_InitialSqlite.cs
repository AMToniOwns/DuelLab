using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DuelLab.Infrastructure.Persistence.SqliteMigrations
{
    /// <inheritdoc />
    public partial class InitialSqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "banlists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FirstSeenAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ProviderVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ProviderUpdatedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    ContentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_banlists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExternalId = table.Column<long>(type: "INTEGER", nullable: false),
                    AlternateExternalIds = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TypeLine = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsExtraDeckCard = table.Column<bool>(type: "INTEGER", nullable: false),
                    Attribute = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Race = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Archetype = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Level = table.Column<int>(type: "INTEGER", nullable: true),
                    Attack = table.Column<int>(type: "INTEGER", nullable: true),
                    Defense = table.Column<int>(type: "INTEGER", nullable: true),
                    LinkValue = table.Column<int>(type: "INTEGER", nullable: true),
                    ImageUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SmallImageUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TcgBanStatus = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    LastSyncedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "decks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Format = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    BanlistId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_decks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_decks_banlists_BanlistId",
                        column: x => x.BanlistId,
                        principalTable: "banlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "banlist_entries",
                columns: table => new
                {
                    BanlistId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
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

            migrationBuilder.CreateTable(
                name: "deck_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeckId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VersionName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ParentVersionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deck_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_deck_versions_deck_versions_ParentVersionId",
                        column: x => x.ParentVersionId,
                        principalTable: "deck_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_deck_versions_decks_DeckId",
                        column: x => x.DeckId,
                        principalTable: "decks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "deck_version_card_roles",
                columns: table => new
                {
                    DeckVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CustomName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deck_version_card_roles", x => new { x.DeckVersionId, x.CardId, x.Kind, x.CustomName });
                    table.ForeignKey(
                        name: "FK_deck_version_card_roles_cards_CardId",
                        column: x => x.CardId,
                        principalTable: "cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_deck_version_card_roles_deck_versions_DeckVersionId",
                        column: x => x.DeckVersionId,
                        principalTable: "deck_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "deck_version_cards",
                columns: table => new
                {
                    DeckVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    Zone = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deck_version_cards", x => new { x.DeckVersionId, x.CardId, x.Zone });
                    table.CheckConstraint("ck_deck_version_cards_quantity", "\"Quantity\" BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_deck_version_cards_cards_CardId",
                        column: x => x.CardId,
                        principalTable: "cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_deck_version_cards_deck_versions_DeckVersionId",
                        column: x => x.DeckVersionId,
                        principalTable: "deck_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "deck_version_combo_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeckVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Pieces = table.Column<string>(type: "TEXT", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "simulation_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeckId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeckVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    RandomSeed = table.Column<long>(type: "INTEGER", nullable: false),
                    SampleCount = table.Column<int>(type: "INTEGER", nullable: false),
                    OpeningHandSize = table.Column<int>(type: "INTEGER", nullable: false),
                    EngineVersion = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DeckSnapshotHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ConfigurationJson = table.Column<string>(type: "TEXT", nullable: false),
                    ResultsJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_simulation_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_simulation_runs_deck_versions_DeckVersionId",
                        column: x => x.DeckVersionId,
                        principalTable: "deck_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_banlist_entries_CardId",
                table: "banlist_entries",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_banlists_FirstSeenAt",
                table: "banlists",
                column: "FirstSeenAt");

            migrationBuilder.CreateIndex(
                name: "IX_cards_ExternalId",
                table: "cards",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cards_Name",
                table: "cards",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_deck_version_card_roles_CardId",
                table: "deck_version_card_roles",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_deck_version_cards_CardId",
                table: "deck_version_cards",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_deck_version_combo_lines_DeckVersionId_Position",
                table: "deck_version_combo_lines",
                columns: new[] { "DeckVersionId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_deck_versions_DeckId",
                table: "deck_versions",
                column: "DeckId");

            migrationBuilder.CreateIndex(
                name: "IX_deck_versions_ParentVersionId",
                table: "deck_versions",
                column: "ParentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_decks_BanlistId",
                table: "decks",
                column: "BanlistId");

            migrationBuilder.CreateIndex(
                name: "IX_simulation_runs_CreatedAt",
                table: "simulation_runs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_simulation_runs_DeckVersionId_CreatedAt",
                table: "simulation_runs",
                columns: new[] { "DeckVersionId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "banlist_entries");

            migrationBuilder.DropTable(
                name: "deck_version_card_roles");

            migrationBuilder.DropTable(
                name: "deck_version_cards");

            migrationBuilder.DropTable(
                name: "deck_version_combo_lines");

            migrationBuilder.DropTable(
                name: "simulation_runs");

            migrationBuilder.DropTable(
                name: "cards");

            migrationBuilder.DropTable(
                name: "deck_versions");

            migrationBuilder.DropTable(
                name: "decks");

            migrationBuilder.DropTable(
                name: "banlists");
        }
    }
}
