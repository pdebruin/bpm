using Microsoft.EntityFrameworkCore;
using Xrm.Core.Data;
using Xrm.Core.Models;

namespace Bpm.Core.Seeding;

/// <summary>
/// Seeds the ProcessDefinition entity and its fields into XRM.
/// Call this from the host's startup to ensure BPM metadata entity exists.
/// </summary>
public class BpmEntitySeeder
{
    public async Task SeedAsync(XrmDbContext db)
    {
        // Check if ProcessDefinition entity already exists
        if (await db.EntityDefinitions.AnyAsync(e => e.Name == "ProcessDefinition"))
            return;

        var processDef = new EntityDefinition
        {
            Id = Guid.NewGuid(),
            Name = "ProcessDefinition",
            DisplayName = "Process Definition",
            PluralName = "Process Definitions",
            Description = "BPM flow definitions — configures what actions execute on state transitions",
            Icon = "gear",
            SortOrder = 100,
            Domain = "BPM",
            DomainSortOrder = 1
        };

        db.EntityDefinitions.Add(processDef);

        var fields = new List<FieldDefinition>
        {
            new() { Id = Guid.NewGuid(), EntityDefinitionId = processDef.Id, Name = "Name", DisplayName = "Name", DataType = FieldDataType.Text, IsRequired = true, MaxLength = 200, SortOrder = 1 },
            new() { Id = Guid.NewGuid(), EntityDefinitionId = processDef.Id, Name = "Description", DisplayName = "Description", DataType = FieldDataType.Text, MaxLength = 500, SortOrder = 2 },
            new() { Id = Guid.NewGuid(), EntityDefinitionId = processDef.Id, Name = "EntityName", DisplayName = "Entity", DataType = FieldDataType.Text, IsRequired = true, MaxLength = 100, SortOrder = 3 },
            new() { Id = Guid.NewGuid(), EntityDefinitionId = processDef.Id, Name = "FieldName", DisplayName = "Field", DataType = FieldDataType.Text, IsRequired = true, MaxLength = 100, SortOrder = 4 },
            new() { Id = Guid.NewGuid(), EntityDefinitionId = processDef.Id, Name = "FromValue", DisplayName = "From Value", DataType = FieldDataType.Text, MaxLength = 100, SortOrder = 5 },
            new() { Id = Guid.NewGuid(), EntityDefinitionId = processDef.Id, Name = "ToValue", DisplayName = "To Value", DataType = FieldDataType.Text, MaxLength = 100, SortOrder = 6 },
            new() { Id = Guid.NewGuid(), EntityDefinitionId = processDef.Id, Name = "StepsJson", DisplayName = "Steps (JSON)", DataType = FieldDataType.RichText, SortOrder = 7 },
            new() { Id = Guid.NewGuid(), EntityDefinitionId = processDef.Id, Name = "Enabled", DisplayName = "Enabled", DataType = FieldDataType.Boolean, DefaultValue = "true", SortOrder = 8 },
            new() { Id = Guid.NewGuid(), EntityDefinitionId = processDef.Id, Name = "Blocking", DisplayName = "Blocking", DataType = FieldDataType.Boolean, DefaultValue = "false", SortOrder = 9 },
        };

        db.FieldDefinitions.AddRange(fields);

        processDef.PrimaryFieldId = fields.First(f => f.Name == "Name").Id;

        await db.SaveChangesAsync();
    }
}
