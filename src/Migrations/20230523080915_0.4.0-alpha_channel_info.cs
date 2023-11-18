using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CookieClicker.Migrations
{
    /// <inheritdoc />
    public partial class _040alpha_channel_info : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "clicks",
                table: "cookies",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,0)");

            migrationBuilder.AddColumn<decimal>(
                name: "channel_id",
                table: "cookies",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "guild_id",
                table: "cookies",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "message_id",
                table: "cookies",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "channel_id",
                table: "cookies");

            migrationBuilder.DropColumn(
                name: "guild_id",
                table: "cookies");

            migrationBuilder.DropColumn(
                name: "message_id",
                table: "cookies");

            migrationBuilder.AlterColumn<decimal>(
                name: "clicks",
                table: "cookies",
                type: "numeric(20,0)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");
        }
    }
}
