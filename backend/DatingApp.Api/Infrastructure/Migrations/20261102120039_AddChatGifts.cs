using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatingApp.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChatGifts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GiftCode",
                table: "Messages",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GiftCode",
                table: "Messages");
        }
    }
}
