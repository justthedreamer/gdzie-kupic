using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gdzie.Kupic.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedLocationAddressDisplayName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AddressDisplayName",
                table: "SavedLocations",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddressDisplayName",
                table: "SavedLocations");
        }
    }
}
