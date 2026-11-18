using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notas.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserPreferences",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    EditorFont = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FontSize = table.Column<double>(type: "double precision", nullable: false),
                    LineHeight = table.Column<double>(type: "double precision", nullable: false),
                    ColumnWidth = table.Column<int>(type: "integer", nullable: false),
                    Accent = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferences", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserPreferences_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserPreferences");
        }
    }
}
