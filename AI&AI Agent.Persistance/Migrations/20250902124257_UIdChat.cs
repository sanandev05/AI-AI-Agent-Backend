using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI_AI_Agent.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class UIdChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Chats_ChatId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ChatId",
                table: "Messages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Chats",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Chats");

            migrationBuilder.RenameColumn(
                name: "TokenUsed",
                table: "Messages",
                newName: "TotalToken");

            migrationBuilder.AddColumn<Guid>(
                name: "ChatMessageUId",
                table: "Messages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InputToken",
                table: "Messages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OutputToken",
                table: "Messages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "MessageUId",
                table: "Chats",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_Chats",
                table: "Chats",
                column: "MessageUId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ChatMessageUId",
                table: "Messages",
                column: "ChatMessageUId");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Chats_ChatMessageUId",
                table: "Messages",
                column: "ChatMessageUId",
                principalTable: "Chats",
                principalColumn: "MessageUId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Chats_ChatMessageUId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ChatMessageUId",
                table: "Messages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Chats",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "ChatMessageUId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "InputToken",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "OutputToken",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "MessageUId",
                table: "Chats");

            migrationBuilder.RenameColumn(
                name: "TotalToken",
                table: "Messages",
                newName: "TokenUsed");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "Chats",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Chats",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Chats",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Chats",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddPrimaryKey(
                name: "PK_Chats",
                table: "Chats",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ChatId",
                table: "Messages",
                column: "ChatId");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Chats_ChatId",
                table: "Messages",
                column: "ChatId",
                principalTable: "Chats",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
