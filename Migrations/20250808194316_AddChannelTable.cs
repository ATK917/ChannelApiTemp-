using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChannelApiTemp.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Channels",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Subscribers",
                table: "Channels",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "Channels");

            migrationBuilder.DropColumn(
                name: "Subscribers",
                table: "Channels");
        }
    }
}
