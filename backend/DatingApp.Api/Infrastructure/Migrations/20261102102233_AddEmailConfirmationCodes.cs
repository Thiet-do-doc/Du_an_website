using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatingApp.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailConfirmationCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConfirmationCodeExpiresAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConfirmationCodeFailures",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte[]>(
                name: "ConfirmationCodeHash",
                table: "AspNetUsers",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfirmationCodeExpiresAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ConfirmationCodeFailures",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ConfirmationCodeHash",
                table: "AspNetUsers");
        }
    }
}
