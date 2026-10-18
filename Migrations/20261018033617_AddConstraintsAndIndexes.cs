using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VolleyballStatTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddConstraintsAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sets_MatchId",
                table: "Sets");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Players",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Opponent",
                table: "Matches",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StatEntries_Grade",
                table: "StatEntries",
                sql: "[Grade] IS NULL OR [Grade] BETWEEN 0 AND 3");

            migrationBuilder.CreateIndex(
                name: "IX_Sets_MatchId_SetNumber",
                table: "Sets",
                columns: new[] { "MatchId", "SetNumber" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sets_SetNumber",
                table: "Sets",
                sql: "[SetNumber] BETWEEN 1 AND 5");

            migrationBuilder.CreateIndex(
                name: "IX_Players_ShirtNumber",
                table: "Players",
                column: "ShirtNumber",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Players_ShirtNumber",
                table: "Players",
                sql: "[ShirtNumber] BETWEEN 0 AND 99");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StatEntries_Grade",
                table: "StatEntries");

            migrationBuilder.DropIndex(
                name: "IX_Sets_MatchId_SetNumber",
                table: "Sets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sets_SetNumber",
                table: "Sets");

            migrationBuilder.DropIndex(
                name: "IX_Players_ShirtNumber",
                table: "Players");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Players_ShirtNumber",
                table: "Players");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Players",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Opponent",
                table: "Matches",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_Sets_MatchId",
                table: "Sets",
                column: "MatchId");
        }
    }
}
