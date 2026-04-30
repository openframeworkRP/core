using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenFramework.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHairAndBeardAppearance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HairColor",
                table: "Characters",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "#3a2a1c");

            migrationBuilder.AddColumn<string>(
                name: "BeardColor",
                table: "Characters",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "#3a2a1c");

            migrationBuilder.AddColumn<string>(
                name: "HairStyle",
                table: "Characters",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BeardStyle",
                table: "Characters",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "HairColor", table: "Characters");
            migrationBuilder.DropColumn(name: "BeardColor", table: "Characters");
            migrationBuilder.DropColumn(name: "HairStyle", table: "Characters");
            migrationBuilder.DropColumn(name: "BeardStyle", table: "Characters");
        }
    }
}
