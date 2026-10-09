using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactForge.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMusicQuestionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Artist",
                table: "Slides",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MusicFilePath",
                table: "Slides",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Slides",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Artist",
                table: "Slides");

            migrationBuilder.DropColumn(
                name: "MusicFilePath",
                table: "Slides");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Slides");
        }
    }
}
