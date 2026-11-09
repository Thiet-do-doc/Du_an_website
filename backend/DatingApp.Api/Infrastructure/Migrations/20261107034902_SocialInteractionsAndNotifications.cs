using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatingApp.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SocialInteractionsAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "AspNetUserRoles" ur USING "AspNetRoles" r
                WHERE ur."RoleId" = r."Id" AND r."NormalizedName" = 'USER'
                AND EXISTS (SELECT 1 FROM "AspNetUserRoles" ar JOIN "AspNetRoles" a ON ar."RoleId" = a."Id"
                    WHERE ar."UserId" = ur."UserId" AND a."NormalizedName" = 'ADMIN');
                """);
            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "PostComments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ConversationId",
                table: "Notifications",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "ActorId",
                table: "Notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Text",
                table: "Notifications",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CommentReactions",
                columns: table => new
                {
                    CommentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommentReactions", x => new { x.CommentId, x.UserId });
                    table.CheckConstraint("CK_CommentReactions_Kind", "\"Kind\" IN ('like', 'haha', 'angry', 'sad')");
                    table.ForeignKey(
                        name: "FK_CommentReactions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommentReactions_PostComments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "PostComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PostComments_ParentId",
                table: "PostComments",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_CommentReactions_UserId",
                table: "CommentReactions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PostComments_PostComments_ParentId",
                table: "PostComments",
                column: "ParentId",
                principalTable: "PostComments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Social notifications have no conversation and cannot exist in the older schema.
            migrationBuilder.Sql("DELETE FROM \"Notifications\" WHERE \"ConversationId\" IS NULL");
            migrationBuilder.DropForeignKey(
                name: "FK_PostComments_PostComments_ParentId",
                table: "PostComments");

            migrationBuilder.DropTable(
                name: "CommentReactions");

            migrationBuilder.DropIndex(
                name: "IX_PostComments_ParentId",
                table: "PostComments");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "PostComments");

            migrationBuilder.DropColumn(
                name: "ActorId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Text",
                table: "Notifications");

            migrationBuilder.AlterColumn<Guid>(
                name: "ConversationId",
                table: "Notifications",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
