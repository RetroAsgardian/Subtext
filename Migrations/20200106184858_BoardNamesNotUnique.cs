using Microsoft.EntityFrameworkCore.Migrations;

namespace Subtext.Migrations
{
    public partial class BoardNamesNotUnique : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Boards_Name",
                table: "Boards");

            migrationBuilder.CreateIndex(
                name: "IX_Boards_Name",
                table: "Boards",
                column: "Name");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Boards_Name",
                table: "Boards");

            migrationBuilder.CreateIndex(
                name: "IX_Boards_Name",
                table: "Boards",
                column: "Name",
                unique: true,
                filter: "[Name] IS NOT NULL");
        }
    }
}
