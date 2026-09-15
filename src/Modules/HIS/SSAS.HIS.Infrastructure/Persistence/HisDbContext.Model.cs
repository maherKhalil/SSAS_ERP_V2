using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.BuildingBlocks.Domain;
using System.Reflection;
using System.Linq;
using System;

namespace SSAS.HIS.Infrastructure.Persistence;

public partial class HisDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            var clrType = entityType.ClrType;
            
            if (entityType.FindPrimaryKey() != null) continue;

            bool hasEntityKey = false;
            var baseType = clrType.BaseType;
            while (baseType != null)
            {
                if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(Entity<>))
                {
                    modelBuilder.Entity(clrType).HasKey("Id");
                    hasEntityKey = true;
                    break;
                }
                baseType = baseType.BaseType;
            }

            if (hasEntityKey) continue;

            var props = clrType.GetProperties();
            var idProp = props.FirstOrDefault(p => p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase)) ??
                         props.FirstOrDefault(p => p.Name.Equals(clrType.Name + "Id", StringComparison.OrdinalIgnoreCase)) ??
                         props.FirstOrDefault(p => p.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase));
            
            if (idProp != null)
            {
                modelBuilder.Entity(clrType).HasKey(idProp.Name);
            }
            else
            {
                modelBuilder.Entity(clrType).HasNoKey();
            }
        }
    }
}
