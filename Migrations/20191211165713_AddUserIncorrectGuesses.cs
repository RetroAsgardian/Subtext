using Microsoft.EntityFrameworkCore.Migrations;

namespace Subtext.Migrations
{
    public partial class AddUserIncorrectGuesses : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IncorrectGuesses",
                table: "Users",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IncorrectGuesses",
                table: "Users");
        }
    }
}
