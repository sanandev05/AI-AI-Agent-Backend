using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI_AI_Agent.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class NewValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrls",
                table: "Messages",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrls",
                table: "Messages");
        }
    }
}
