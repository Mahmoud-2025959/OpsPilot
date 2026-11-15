using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsPilot.Infrastructure.Migrations
{
    /// <summary>
    /// Brings user names left behind by the old rule into line with the current one.
    /// </summary>
    /// <remarks>
    /// <see cref="Domain.Entities.User"/> takes the user name from the email, but accounts
    /// created before that rule carried the display name instead — which is both the wrong value
    /// and, for two people who share a name, a collision the global unique index over the
    /// normalized user name rejects. Sign-in is by email alone (ERD §3), so rewriting the column
    /// cannot lock anybody out; it only makes the stored value agree with the one the domain now
    /// writes for every new and every re-signed-up account.
    /// </remarks>
    public partial class BackfillUserNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // One statement over every row rather than a fix per row: the value being written is
            // derived from what is already there, and rows that already agree are left untouched
            // so re-running the migration (or scripting it for another environment) is harmless.
            migrationBuilder.Sql("""
                UPDATE [Users]
                SET [UserName] = [Email],
                    [NormalizedUserName] = UPPER([Email])
                WHERE [Email] IS NOT NULL
                  AND LTRIM(RTRIM([Email])) <> ''
                  AND ([UserName] <> [Email]
                       OR [NormalizedUserName] <> UPPER([Email]));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to restore: the column only ever held a display name copied by a rule that
            // no longer exists, and the rows that rule produced cannot be told apart from ones
            // a person may have chosen later. Rolling back would mean inventing values.
        }
    }
}
