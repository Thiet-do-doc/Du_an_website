using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatingApp.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPostMediaAndInteractions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Feeling",
                table: "Posts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "PhotoContent",
                table: "Posts",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoType",
                table: "Posts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PostComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostComments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PostComments_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostLikes",
                columns: table => new
                {
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostLikes", x => new { x.PostId, x.UserId });
                    table.ForeignKey(
                        name: "FK_PostLikes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PostLikes_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PostComments_PostId_CreatedAtUtc_Id",
                table: "PostComments",
                columns: new[] { "PostId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PostComments_UserId",
                table: "PostComments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PostLikes_UserId",
                table: "PostLikes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PostComments");

            migrationBuilder.DropTable(
                name: "PostLikes");

            migrationBuilder.DropColumn(
                name: "Feeling",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "PhotoContent",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "PhotoType",
                table: "Posts");
        }
    }
}
