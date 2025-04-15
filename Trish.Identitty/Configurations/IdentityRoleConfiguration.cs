/*
 * using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Trish.Identitty.Configurations
{
    public class IdentityRoleConfiguration
    {
        public static void Configure(EntityTypeBuilder<IdentityRole> builder) => builder.HasData(
                new IdentityRole()
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Admin",
                    ConcurrencyStamp = "1",
                    NormalizedName = "ADMIN",
                },
                new IdentityRole()
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "User",
                    ConcurrencyStamp = "2",
                    NormalizedName = "USER",
                }
            );
    }
}
*/
