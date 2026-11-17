using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatingApp.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PostReportEvidenceAndModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EvidenceContent",
                table: "Reports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceFeeling",
                table: "Reports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "EvidencePhoto",
                table: "Reports",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidencePhotoType",
                table: "Reports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "Posts",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EvidenceContent",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "EvidenceFeeling",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "EvidencePhoto",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "EvidencePhotoType",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "Posts");
        }
    }
}
