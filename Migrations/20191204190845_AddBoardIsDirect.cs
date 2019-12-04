using Microsoft.EntityFrameworkCore.Migrations;

namespace Subtext.Migrations {
	public partial class AddBoardIsDirect : Migration {
		protected override void Up(MigrationBuilder migrationBuilder) {
			migrationBuilder.AddColumn<bool>(
				name: "IsDirect",
				table: "Boards",
				nullable: false,
				defaultValue: false);
		}

		protected override void Down(MigrationBuilder migrationBuilder) {
			migrationBuilder.DropColumn(
				name: "IsDirect",
				table: "Boards");
		}
	}
}
