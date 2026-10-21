using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using SldBuilder.Core.Mapping;
using SldBuilder.Core.Models;

namespace SldBuilder.Acad.Drawing;

/// <param name="Block">Block name.</param>
/// <param name="Group">1-based group number, null for TOTAL/KNF/INCOMER.</param>
public sealed record DrawWarning(string Block, int? Group, string Property, string Value);

public sealed record DrawResult(int Erased, int GroupsDrawn, IReadOnlyList<DrawWarning> Warnings);

public sealed class MissingBlocksException(IReadOnlyList<string> blocks)
    : Exception(string.Join(", ", blocks))
{
    public IReadOnlyList<string> Blocks { get; } = blocks;
}

/// <summary>
/// Erases the generated blocks from the drawing and inserts them again from <see cref="SldData"/>.
/// The caller must lock the document.
/// </summary>
public sealed class SldDrawer(Database db)
{
    private readonly List<DrawWarning> _warnings = [];

    public DrawResult Draw(SldData data)
    {
        _warnings.Clear();

        // AttributeReference.AdjustAlignment works against the working database.
        Database previous = HostApplicationServices.WorkingDatabase;
        HostApplicationServices.WorkingDatabase = db;
        try
        {
            using var tr = db.TransactionManager.StartTransaction();
            var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

            var missing = BlockNames.Generated.Where(name => !blockTable.Has(name)).ToList();
            if (missing.Count > 0)
                throw new MissingBlocksException(missing);

            int erased = EraseGenerated(tr, blockTable);

            var modelSpace = (BlockTableRecord)tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            var origin = Point3d.Origin;

            Insert(tr, blockTable, modelSpace, BlockNames.Total, origin, data.Totals.ToAttributes());

            if (data.Balance.IsRequired)
                Insert(tr, blockTable, modelSpace, BlockNames.Balance, origin, data.Balance.ToAttributes());

            Insert(tr, blockTable, modelSpace, BlockNames.Incomer, origin, data.Incomer.ToAttributes(),
                DynamicProperties.IncomerType, data.Incomer.IncomerType);

            foreach (var group in data.Groups)
            {
                var position = new Point3d((group.Index - 1) * Defaults.GroupStepX, 0, 0);
                Insert(tr, blockTable, modelSpace, BlockNames.Automat, position, group.ToAutomatAttributes(),
                    DynamicProperties.BreakerType, group.BreakerType, group.Index);
                Insert(tr, blockTable, modelSpace, BlockNames.Line, position, group.ToLineAttributes(),
                    DynamicProperties.Consumer, group.ConsumerType, group.Index);
            }

            tr.Commit();
            return new DrawResult(erased, data.Groups.Count, _warnings.ToList());
        }
        finally
        {
            HostApplicationServices.WorkingDatabase = previous;
        }
    }

    /// <summary>Erases generated blocks in model space and on all layouts; other objects stay.</summary>
    private static int EraseGenerated(Transaction tr, BlockTable blockTable)
    {
        int count = 0;
        foreach (ObjectId btrId in blockTable)
        {
            var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
            if (!btr.IsLayout)
                continue;

            foreach (ObjectId id in btr)
            {
                if (id.ObjectClass.DxfName != "INSERT")
                    continue;
                var reference = (BlockReference)tr.GetObject(id, OpenMode.ForRead);
                if (!BlockNames.Generated.Contains(EffectiveName(tr, reference)))
                    continue;
                reference.UpgradeOpen();
                reference.Erase();
                count++;
            }
        }
        return count;
    }

    private void Insert(
        Transaction tr,
        BlockTable blockTable,
        BlockTableRecord space,
        string blockName,
        Point3d position,
        IReadOnlyDictionary<string, string> attributes,
        string? visibilityProperty = null,
        string? visibilityValue = null,
        int? group = null)
    {
        var definitionId = blockTable[blockName];
        var definition = (BlockTableRecord)tr.GetObject(definitionId, OpenMode.ForRead);

        var reference = new BlockReference(position, definitionId);
        space.AppendEntity(reference);
        tr.AddNewlyCreatedDBObject(reference, true);

        // Attributes are matched by tag, not by their order in the block.
        foreach (ObjectId id in definition)
        {
            if (tr.GetObject(id, OpenMode.ForRead) is not AttributeDefinition { Constant: false } attDef)
                continue;

            var attRef = new AttributeReference();
            attRef.SetAttributeFromBlock(attDef, reference.BlockTransform);
            if (attributes.TryGetValue(attDef.Tag, out var text))
                attRef.TextString = text;
            if (!attDef.IsMTextAttributeDefinition)
                attRef.AdjustAlignment(db);

            reference.AttributeCollection.AppendAttribute(attRef);
            tr.AddNewlyCreatedDBObject(attRef, true);
        }

        if (visibilityProperty is not null)
            SetVisibility(reference, blockName, visibilityProperty, visibilityValue ?? string.Empty, group);
    }

    private void SetVisibility(BlockReference reference, string blockName, string property, string value, int? group)
    {
        foreach (DynamicBlockReferenceProperty prop in reference.DynamicBlockReferencePropertyCollection)
        {
            if (prop.PropertyName != property)
                continue;

            if (prop.GetAllowedValues().Any(allowed => allowed?.ToString() == value))
                prop.Value = value;
            else
                _warnings.Add(new DrawWarning(blockName, group, property, value));
            return;
        }

        _warnings.Add(new DrawWarning(blockName, group, property, value));
    }

    private static string EffectiveName(Transaction tr, BlockReference reference)
    {
        var id = reference.IsDynamicBlock ? reference.DynamicBlockTableRecord : reference.BlockTableRecord;
        return ((BlockTableRecord)tr.GetObject(id, OpenMode.ForRead)).Name;
    }
}
