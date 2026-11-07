using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DuelLab.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CardAlternatePasscodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long[]>(
                name: "AlternateExternalIds",
                table: "cards",
                type: "bigint[]",
                nullable: false,
                defaultValue: new long[0]);

            migrationBuilder.CreateIndex(
                name: "IX_cards_AlternateExternalIds",
                table: "cards",
                column: "AlternateExternalIds")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_cards_AlternateExternalIds",
                table: "cards");

            migrationBuilder.DropColumn(
                name: "AlternateExternalIds",
                table: "cards");
        }
    }
}
