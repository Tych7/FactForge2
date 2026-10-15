using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactForge.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerScoreSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerScoreSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    SlideId = table.Column<int>(type: "INTEGER", nullable: false),
                    QuestionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerScoreSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerScoreSnapshots_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerScoreSnapshots_Slides_SlideId",
                        column: x => x.SlideId,
                        principalTable: "Slides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerScoreSnapshots_PlayerId_SlideId",
                table: "PlayerScoreSnapshots",
                columns: new[] { "PlayerId", "SlideId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerScoreSnapshots_SlideId",
                table: "PlayerScoreSnapshots",
                column: "SlideId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerScoreSnapshots");
        }
    }
}
