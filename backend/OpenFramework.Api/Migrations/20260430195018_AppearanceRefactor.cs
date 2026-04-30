using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenFramework.Api.Migrations
{
    /// <inheritdoc />
    public partial class AppearanceRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Morphs");

            migrationBuilder.DropColumn(
                name: "HeadIndex",
                table: "Characters");

            // Defaults JSON valides : sans ca les chars existants ont une chaine
            // vide qui fera planter le JsonSerializer cote jeu au premier read.
            migrationBuilder.AddColumn<string>(
                name: "ClothingJson",
                table: "Characters",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "MorphsJson",
                table: "Characters",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClothingJson",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "MorphsJson",
                table: "Characters");

            migrationBuilder.AddColumn<int>(
                name: "HeadIndex",
                table: "Characters",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Morphs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BrowDown = table.Column<float>(type: "real", nullable: false),
                    BrowInnerUp = table.Column<float>(type: "real", nullable: false),
                    BrowOuterUp = table.Column<float>(type: "real", nullable: false),
                    CharacterId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CheekPuff = table.Column<float>(type: "real", nullable: false),
                    CheekSquint = table.Column<float>(type: "real", nullable: false),
                    EyesLookDown = table.Column<float>(type: "real", nullable: false),
                    EyesLookIn = table.Column<float>(type: "real", nullable: false),
                    EyesLookOut = table.Column<float>(type: "real", nullable: false),
                    EyesLookUp = table.Column<float>(type: "real", nullable: false),
                    EyesSquint = table.Column<float>(type: "real", nullable: false),
                    EyesWide = table.Column<float>(type: "real", nullable: false),
                    JawForward = table.Column<float>(type: "real", nullable: false),
                    JawLeft = table.Column<float>(type: "real", nullable: false),
                    JawRight = table.Column<float>(type: "real", nullable: false),
                    MouthDimple = table.Column<float>(type: "real", nullable: false),
                    MouthRollUpper = table.Column<float>(type: "real", nullable: false),
                    MouthStretch = table.Column<float>(type: "real", nullable: false),
                    NoseSneer = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Morphs", x => x.Id);
                });
        }
    }
}
