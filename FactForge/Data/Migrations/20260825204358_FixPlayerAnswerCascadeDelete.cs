using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactForge.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixPlayerAnswerCascadeDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlayerAnswers_Slides_SlideId",
                table: "PlayerAnswers");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerAnswers_Slides_SlideId",
                table: "PlayerAnswers",
                column: "SlideId",
                principalTable: "Slides",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlayerAnswers_Slides_SlideId",
                table: "PlayerAnswers");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerAnswers_Slides_SlideId",
                table: "PlayerAnswers",
                column: "SlideId",
                principalTable: "Slides",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
