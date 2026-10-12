using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DatingApp.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialTV4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DemoUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Alias = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemoUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DemoBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockedId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemoBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DemoBlocks_DemoUsers_BlockedId",
                        column: x => x.BlockedId,
                        principalTable: "DemoUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DemoBlocks_DemoUsers_BlockerId",
                        column: x => x.BlockerId,
                        principalTable: "DemoUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DemoMatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserAId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserBId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemoMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DemoMatches_DemoUsers_UserAId",
                        column: x => x.UserAId,
                        principalTable: "DemoUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DemoMatches_DemoUsers_UserBId",
                        column: x => x.UserBId,
                        principalTable: "DemoUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_DemoUsers_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "DemoUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conversations_DemoMatches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "DemoMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Messages_DemoUsers_SenderId",
                        column: x => x.SenderId,
                        principalTable: "DemoUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_MatchId",
                table: "Conversations",
                column: "MatchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DemoBlocks_BlockedId",
                table: "DemoBlocks",
                column: "BlockedId");

            migrationBuilder.CreateIndex(
                name: "IX_DemoBlocks_BlockerId_BlockedId",
                table: "DemoBlocks",
                columns: new[] { "BlockerId", "BlockedId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DemoMatches_UserAId_UserBId",
                table: "DemoMatches",
                columns: new[] { "UserAId", "UserBId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DemoMatches_UserBId",
                table: "DemoMatches",
                column: "UserBId");

            migrationBuilder.CreateIndex(
                name: "IX_DemoUsers_Alias",
                table: "DemoUsers",
                column: "Alias",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId_SentAt_Id",
                table: "Messages",
                columns: new[] { "ConversationId", "SentAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SenderId_ClientMessageId",
                table: "Messages",
                columns: new[] { "SenderId", "ClientMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientId_CreatedAt_Id",
                table: "Notifications",
                columns: new[] { "RecipientId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientId_ReadAt",
                table: "Notifications",
                columns: new[] { "RecipientId", "ReadAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DemoBlocks");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "Conversations");

            migrationBuilder.DropTable(
                name: "DemoMatches");

            migrationBuilder.DropTable(
                name: "DemoUsers");
        }
    }
}
