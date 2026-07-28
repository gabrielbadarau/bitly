using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bitly.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexOnShortUrlCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ShortUrls_Code",
                table: "ShortUrls",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShortUrls_Code",
                table: "ShortUrls");
        }
    }
}
