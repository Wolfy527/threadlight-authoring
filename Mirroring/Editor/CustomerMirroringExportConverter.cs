namespace Threadlight.Mirroring.Editor
{
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Threadlight.Authoring.Editor;

/// <summary>Creator schema 5 to the released customer schema 3, on export copies only.</summary>
public sealed class CustomerMirroringExportConverter : ICustomerExportDocumentConverter
{
    public string CreatorScriptGuid => "6fb7005cb533460ea3d5abef93a56be0";
    public string CustomerScriptGuid => "5c54d508ba4a3ee4baa5148633885b51";

    public void Convert(CustomerExportDocument document)
    {
        document.RequireVersion("dataVersion", 5);
        document.RetainFields(
            "dataVersion liveMirror mirrorCenter constraintTargetsObjectName addVrcfuryArmatureLinks " +
            "applyScaleReference scaleReference scaleHandles pairs showScenePreview previewSource " +
            "previewMaterial generateThreadlightComponentsBootstrapper threadlightComponentsBootstrapperFolderPath mirrorOptions",
            "targetNamePrefix sourceSideLabel mirroredSideLabel sourceFolderName mirroredFolderName " +
            "removeUnusedGeneratedTargets targetLocalPosition targetLocalEulerRotation targetLocalScale " +
            "applyDefaultTransformToExistingTargets addParentConstraintToPrefabContainer " +
            "prefabContainerParentConstraint prefabContainerParentConstraintCreatedByThreadlight");
        document.Set("dataVersion", "  dataVersion: 3");

        // The creator switch is createOppositeTarget; legacy mirrorEnabled is
        // retained data and is not authoritative in current creator prefabs.
        string pairs = document.Get("pairs");
        string[] blocks = Regex.Split(pairs, @"(?m)(?=^  - mirrorEnabled:)");
        for (int i = 1; i < blocks.Length; i++)
        {
            Match enabled = Regex.Match(blocks[i], @"(?m)^    createOppositeTarget: ([01])\r?$");
            if (!enabled.Success) throw new InvalidOperationException("Unsupported mirroring pair export contract.");
            string pair = Regex.Replace(blocks[i], @"(?m)^  - mirrorEnabled: [01]\r?$",
                "  - mirrorEnabled: " + enabled.Groups[1].Value);
            pair = Regex.Replace(pair,
                @"(?m)^    (createOppositeTarget|useGlobalSideLabels|sourceSideLabel|mirroredSideLabel):[^\n]*\n", "");
            var allowed = new[] { "pairName", "sourceBone", "mirroredBone", "sourceTarget", "mirroredTarget", "mirroredRotationOffset" };
            foreach (Match field in Regex.Matches(pair, @"(?m)^    ([A-Za-z_][A-Za-z_0-9]*):"))
                if (!allowed.Contains(field.Groups[1].Value))
                    throw new InvalidOperationException("Unsupported mirroring pair field: " + field.Groups[1].Value);
            blocks[i] = pair;
        }
        if (blocks.Length == 1 && pairs.Trim() != "pairs: []")
            throw new InvalidOperationException("Unsupported mirroring pair serialization.");
        document.Set("pairs", string.Concat(blocks));

        // Components owns its own preview shader. Custom asset materials retain
        // their references; the creator package's default uses that fallback.
        if (document.Get("previewMaterial").Contains("guid: 1ea3771d93ec4d48b199f1916cbacb30,"))
            document.Set("previewMaterial", "  previewMaterial: {fileID: 0}");
    }
}
}
