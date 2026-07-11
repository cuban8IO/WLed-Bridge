using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace wledBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddControllerControlDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ControllerControlDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ControllerDeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Channel = table.Column<int>(type: "INTEGER", nullable: false),
                    CommandCode = table.Column<int>(type: "INTEGER", nullable: false),
                    DataNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    LedCapability = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsRelative = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControllerControlDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ControllerControlDefinitions_ControllerDevices_ControllerDeviceId",
                        column: x => x.ControllerDeviceId,
                        principalTable: "ControllerDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ControllerControlDefinitions_ControllerDeviceId_Channel_CommandCode_DataNumber",
                table: "ControllerControlDefinitions",
                columns: new[] { "ControllerDeviceId", "Channel", "CommandCode", "DataNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ControllerControlDefinitions");
        }
    }
}
