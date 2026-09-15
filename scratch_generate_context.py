import os
import re

entities_dir = r"c:\Users\User\Documents\SSAS_ERP_V2\SSAS_ERP_V2\src\Modules\HIS\SSAS.HIS.Domain\Entities"

# map from entity name to full namespace
db_sets = []

for root, _, files in os.walk(entities_dir):
    for file in files:
        if file.endswith(".cs"):
            with open(os.path.join(root, file), 'r', encoding='utf-8') as f:
                content = f.read()
                # find namespace
                ns_match = re.search(r'namespace\s+([A-Za-z0-9_.]+)', content)
                ns = ns_match.group(1) if ns_match else ""
                
                # find public class <Name>
                match = re.search(r'public\s+(?:partial\s+)?class\s+([A-Za-z0-9_]+)', content)
                if match:
                    name = match.group(1)
                    full_name = f"{ns}.{name}" if ns else name
                    db_sets.append(f"    public DbSet<{full_name}> {name}s_{ns.split('.')[-1]} => Set<{full_name}>();")

db_sets = sorted(list(set(db_sets)))
db_sets_str = "\n".join(db_sets)

context_code = f"""using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using System.Reflection;

namespace SSAS.HIS.Infrastructure.Persistence;

public class HisDbContext(
    DbContextOptions<HisDbContext> options,
    ICurrentUser currentUser,
    ICurrentTenant currentTenant,
    IDateTimeProvider dateTimeProvider)
    : PersistenceDbContext(options, currentUser, currentTenant, dateTimeProvider)
{{
{db_sets_str}

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {{
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }}
}}
"""

with open(r"c:\Users\User\Documents\SSAS_ERP_V2\SSAS_ERP_V2\src\Modules\HIS\SSAS.HIS.Infrastructure\Persistence\HisDbContext.cs", 'w', encoding='utf-8') as f:
    f.write(context_code)

print("Generated HisDbContext.cs")
