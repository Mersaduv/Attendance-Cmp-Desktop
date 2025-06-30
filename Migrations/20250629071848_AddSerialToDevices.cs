using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttandenceDesktop.Migrations
{
    /// <inheritdoc />
    public partial class AddSerialToDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SerialNumber",
                table: "Devices",
                type: "TEXT",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SerialNumber",
                table: "Devices");
        }
    }
}
