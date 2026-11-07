using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DuelLab.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    ExternalId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TypeLine = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsExtraDeckCard = table.Column<bool>(type: "boolean", nullable: false),
                    Attribute = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Race = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Archetype = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: true),
                    Attack = table.Column<int>(type: "integer", nullable: true),
                    Defense = table.Column<int>(type: "integer", nullable: true),
                    LinkValue = table.Column<int>(type: "integer", nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SmallImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TcgBanStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "decks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Format = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_decks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "deck_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeckId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ParentVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                    DeckVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CardId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CustomName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
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
                    DeckVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CardId = table.Column<int>(type: "integer", nullable: false),
                    Zone = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
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
                name: "simulation_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeckId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeckVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RandomSeed = table.Column<long>(type: "bigint", nullable: false),
                    SampleCount = table.Column<int>(type: "integer", nullable: false),
                    OpeningHandSize = table.Column<int>(type: "integer", nullable: false),
                    EngineVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DeckSnapshotHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ConfigurationJson = table.Column<string>(type: "jsonb", nullable: false),
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
                name: "IX_deck_versions_DeckId",
                table: "deck_versions",
                column: "DeckId");

            migrationBuilder.CreateIndex(
                name: "IX_deck_versions_ParentVersionId",
                table: "deck_versions",
                column: "ParentVersionId");

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
                name: "deck_version_card_roles");

            migrationBuilder.DropTable(
                name: "deck_version_cards");

            migrationBuilder.DropTable(
                name: "simulation_runs");

            migrationBuilder.DropTable(
                name: "cards");

            migrationBuilder.DropTable(
                name: "deck_versions");

            migrationBuilder.DropTable(
                name: "decks");
        }
    }
}
