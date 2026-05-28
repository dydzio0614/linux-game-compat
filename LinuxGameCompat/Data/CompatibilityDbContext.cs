using Microsoft.EntityFrameworkCore;

namespace LinuxGameCompat.Data;

public sealed class CompatibilityDbContext(DbContextOptions<CompatibilityDbContext> options) : DbContext(options)
{
}
