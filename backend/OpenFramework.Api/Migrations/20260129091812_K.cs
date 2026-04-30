using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenFramework.Api.Migrations
{
    /// <inheritdoc />
    public partial class K : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<float>(
                name: "Weight",
                table: "Characters",
                type: "real",
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint");

            migrationBuilder.AlterColumn<float>(
                name: "Height",
                table: "Characters",
                type: "real",
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint");

            migrationBuilder.AddColumn<int>(
                name: "CountryWhereFrom",
                table: "Characters",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                table: "Characters",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "Morphs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CharacterId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BrowDown = table.Column<float>(type: "real", nullable: false),
                    BrowInnerUp = table.Column<float>(type: "real", nullable: false),
                    BrowOuterUp = table.Column<float>(type: "real", nullable: false),
                    EyesLookDown = table.Column<float>(type: "real", nullable: false),
                    EyesLookIn = table.Column<float>(type: "real", nullable: false),
                    EyesLookOut = table.Column<float>(type: "real", nullable: false),
                    EyesLookUp = table.Column<float>(type: "real", nullable: false),
                    EyesSquint = table.Column<float>(type: "real", nullable: false),
                    EyesWide = table.Column<float>(type: "real", nullable: false),
                    CheekPuff = table.Column<float>(type: "real", nullable: false),
                    CheekSquint = table.Column<float>(type: "real", nullable: false),
                    NoseSneer = table.Column<float>(type: "real", nullable: false),
                    JawForward = table.Column<float>(type: "real", nullable: false),
                    JawLeft = table.Column<float>(type: "real", nullable: false),
                    JawRight = table.Column<float>(type: "real", nullable: false),
                    MouthDimple = table.Column<float>(type: "real", nullable: false),
                    MouthRollUpper = table.Column<float>(type: "real", nullable: false),
                    MouthStretch = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Morphs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Morphs");

            migrationBuilder.DropColumn(
                name: "CountryWhereFrom",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Characters");

            migrationBuilder.AlterColumn<short>(
                name: "Weight",
                table: "Characters",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(float),
                oldType: "real");

            migrationBuilder.AlterColumn<short>(
                name: "Height",
                table: "Characters",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(float),
                oldType: "real");
        }
    }
}
