using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactForge.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNumbersOnlyToSlide : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NumbersOnly",
                table: "Slides",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NumbersOnly",
                table: "Slides");
        }
    }
}
