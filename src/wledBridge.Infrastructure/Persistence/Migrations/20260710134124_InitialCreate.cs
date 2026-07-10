using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace wledBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ControllerDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DriverKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    MidiInputDeviceName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    MidiOutputDeviceName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControllerDevices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ControlMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ControllerDeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PhysicalControlId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    VirtualControlId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VirtualMixers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualMixers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WledDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Host = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WledDevices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VirtualControls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    VirtualMixerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Value = table.Column<double>(type: "REAL", nullable: false),
                    IsOn = table.Column<bool>(type: "INTEGER", nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualControls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VirtualControls_VirtualMixers_VirtualMixerId",
                        column: x => x.VirtualMixerId,
                        principalTable: "VirtualMixers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ControlMappings_ControllerDeviceId_PhysicalControlId",
                table: "ControlMappings",
                columns: new[] { "ControllerDeviceId", "PhysicalControlId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VirtualControls_VirtualMixerId",
                table: "VirtualControls",
                column: "VirtualMixerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ControllerDevices");

            migrationBuilder.DropTable(
                name: "ControlMappings");

            migrationBuilder.DropTable(
                name: "VirtualControls");

            migrationBuilder.DropTable(
                name: "WledDevices");

            migrationBuilder.DropTable(
                name: "VirtualMixers");
        }
    }
}
