using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VolleyballStatTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchFinishedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FinishedAt",
                table: "Matches",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinishedAt",
                table: "Matches");
        }
    }
}
