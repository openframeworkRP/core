using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenFramework.Api.Migrations
{
    /// <inheritdoc />
    public partial class Inventaire : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "YSlot",
                table: "Items",
                newName: "Line");

            migrationBuilder.RenameColumn(
                name: "XSlot",
                table: "Items",
                newName: "Collum");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Line",
                table: "Items",
                newName: "YSlot");

            migrationBuilder.RenameColumn(
                name: "Collum",
                table: "Items",
                newName: "XSlot");
        }
    }
}
