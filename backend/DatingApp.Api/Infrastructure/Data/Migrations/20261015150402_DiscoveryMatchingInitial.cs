using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatingApp.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DiscoveryMatchingInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserLowId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserHighId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UnmatchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.Id);
                    table.CheckConstraint("CK_Matches_UserOrder", "\"UserLowId\" < \"UserHighId\"");
                });

            migrationBuilder.CreateTable(
                name: "Passes",
                columns: table => new
                {
                    FromUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Passes", x => new { x.FromUserId, x.ToUserId });
                    table.CheckConstraint("CK_Passes_NoSelfPass", "\"FromUserId\" <> \"ToUserId\"");
                });

            migrationBuilder.CreateTable(
                name: "UserLikes",
                columns: table => new
                {
                    FromUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLikes", x => new { x.FromUserId, x.ToUserId });
                    table.CheckConstraint("CK_UserLikes_NoSelfLike", "\"FromUserId\" <> \"ToUserId\"");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_UserHighId_UnmatchedAt",
                table: "Matches",
                columns: new[] { "UserHighId", "UnmatchedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_UserLowId_UnmatchedAt",
                table: "Matches",
                columns: new[] { "UserLowId", "UnmatchedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Matches_UserLowId_UserHighId",
                table: "Matches",
                columns: new[] { "UserLowId", "UserHighId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLikes_ToUserId_FromUserId",
                table: "UserLikes",
                columns: new[] { "ToUserId", "FromUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Matches");

            migrationBuilder.DropTable(
                name: "Passes");

            migrationBuilder.DropTable(
                name: "UserLikes");
        }
    }
}
