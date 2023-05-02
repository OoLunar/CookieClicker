using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CookieClicker.Migrations
{
    /// <inheritdoc />
    public partial class _010 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cookies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    clicks = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cookies", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cookies");
        }
    }
}
