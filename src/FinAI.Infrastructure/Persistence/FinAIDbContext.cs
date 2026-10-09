using FinAI.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FinAI.Infrastructure.Persistence;

public sealed class FinAIDbContext(DbContextOptions<FinAIDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
}
