using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsPilot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKeyCiphertext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KeyCiphertext",
                table: "ApiKeys",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KeyCiphertext",
                table: "ApiKeys");
        }
    }
}
