[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(
    "Threadlight.Development.Tests.Editor")]

namespace Threadlight.Authoring.Editor
{
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
/// <summary>
/// Creates the ownership-marked, creator-exportable fallback installer. The
/// generated bootstrap keeps a registered VPM package authoritative.
/// </summary>
public static class ThreadlightComponentsBootstrapExportUtility
{
    public const string DefaultFolderPath = "Assets/Threadlight/Components/Temp";
    private const string InstallerFolderName = "Threadlight Components Installer";
    private const string BootstrapFileName = "ThreadlightComponentsBootstrap.cs";
    private const string PayloadFileName = "ThreadlightComponentsFallback.bytes";
    private const string OwnershipMarkerFileName = "Threadlight Components Bootstrapper.marker";
    private const string OwnershipMarkerContents = "Threadlight Components temporary export bootstrapper";
    private const string AuthoringRetentionSessionKeyPrefix =
        "Threadlight.Components.AuthoringExport.";
    private const string TemplateRelativePath = "Distribution~/ThreadlightComponentsBootstrap.cs.template";
    private const string PayloadRelativePath =
        "Distribution~/ThreadlightComponentsFallback.bytes";
    /// <summary>Resolves the owned installer path without changing assets.</summary>
    public static bool TryGetInstallerPath(string folderPath, out string installerPath, out string error)
    {
        installerPath = string.Empty;
        if (!TryNormalizeFolderPath(folderPath, out string folder, out error))
            return false;
        installerPath = folder + "/" + InstallerFolderName;
        return true;
    }
    /// <summary>Creates or updates the installer below an Assets folder.</summary>
    public static bool TryCreateOrUpdate(string folderPath, out string installerPath, out string error)
    {
        return TryCreateOrUpdateCore(folderPath, out installerPath, out error);
    }
    internal static bool TryCreateDistributionSnapshot(string installerPath,
        out string bootstrapSource, out byte[] payload, out string error)
    {
        bootstrapSource = string.Empty;
        payload = null;
        error = string.Empty;
        try
        {
            string packageRoot = GetAuthoringCorePackageRoot();
            string templatePath = Path.Combine(packageRoot,
                TemplateRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string payloadPath = Path.Combine(packageRoot,
                PayloadRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string template = File.ReadAllText(templatePath);
            ValidateBootstrapTemplate(template);
            payload = File.ReadAllBytes(payloadPath);
            bootstrapSource = CreateBootstrapSource(template, installerPath,
                "00000000000000000000000000000000");
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }
    private static bool TryCreateOrUpdateCore(string folderPath,
        out string installerPath, out string error)
    {
        if (!TryGetInstallerPath(folderPath, out installerPath, out error))
            return false;
        ExportPlan plan = null;
        try
        {
            plan = new ExportPlan(installerPath);
            if (!plan.TryLoadTemplate(out error))
                return false;
            WriteAuthoringRetentionMarker(installerPath);
            plan.Execute();
            // The generated bootstrap compiles inside Assets. Mark this exact
            // source-project copy before refresh so it cannot clean itself up
            // while the creator or export pipeline is still reading it. These
            // local markers are not included in exported packages.
            SessionState.SetBool(
                AuthoringRetentionSessionKeyPrefix + installerPath, true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return true;
        }
        catch (Exception exception)
        {
            TryRemoveIncompleteInstaller(plan != null && plan.CreatedInstaller,
                installerPath);
            error = exception is UnauthorizedAccessException ? exception.Message :
                "ThreadLight Components could not create the temporary bootstrapper:\n" +
                exception.Message;
            return false;
        }
    }
    /// <summary>Immutable paths define one ownership-safe export transaction.</summary>
    private sealed class ExportPlan
    {
        private readonly string installerAssetPath, templatePath, sourcePayloadPath;
        private readonly string editorAssetPath, installerPath, payloadPath;
        private readonly string bootstrapPath, markerPath;
        private string template;
        internal bool CreatedInstaller { get; private set; }
        internal ExportPlan(string installerAssetPath)
        {
            this.installerAssetPath = installerAssetPath;
            string packageRoot = GetAuthoringCorePackageRoot();
            templatePath = Path.Combine(packageRoot,
                TemplateRelativePath.Replace('/', Path.DirectorySeparatorChar));
            sourcePayloadPath = Path.Combine(packageRoot,
                PayloadRelativePath.Replace('/', Path.DirectorySeparatorChar));
            editorAssetPath = installerAssetPath + "/Editor";
            installerPath = ToAbsolutePath(installerAssetPath);
            payloadPath = Path.Combine(installerPath, PayloadFileName);
            bootstrapPath = Path.Combine(installerPath, "Editor", BootstrapFileName);
            markerPath = Path.Combine(installerPath, OwnershipMarkerFileName);
        }
        internal bool TryLoadTemplate(out string error)
        {
            error = string.Empty;
            if (!File.Exists(templatePath))
            {
                error = "ThreadLight Authoring could not find its customer " +
                        "bootstrapper template. Reinstall or update the authoring package.";
                return false;
            }
            if (!File.Exists(sourcePayloadPath))
            {
                error = "ThreadLight Authoring could not find its bundled customer " +
                        "support payload. Reinstall or update the authoring package.";
                return false;
            }
            template = File.ReadAllText(templatePath);
            ValidateBootstrapTemplate(template);
            return true;
        }
        internal void Execute()
        {
            string parent = GetParentAssetPath(installerAssetPath);
            EnsureFolderPath(parent);
            bool exists = AssetDatabase.IsValidFolder(installerAssetPath);
            if (exists && !HasOwnershipMarker(markerPath))
                throw new UnauthorizedAccessException(
                    "ThreadLight Components refused to replace an existing installer " +
                    "folder it does not own:\n" + installerAssetPath);
            if (!exists)
            {
                AssetDatabase.CreateFolder(parent, InstallerFolderName);
                if (!AssetDatabase.IsValidFolder(installerAssetPath))
                    throw new IOException("Unity could not create the installer folder.");
                CreatedInstaller = true;
            }
            if (!AssetDatabase.IsValidFolder(editorAssetPath))
                AssetDatabase.CreateFolder(installerAssetPath, "Editor");

            string payloadGuid = EnsureMetaGuid(payloadPath, "TextScriptImporter:");
            EnsureMetaGuid(bootstrapPath, "MonoImporter:");
            CopyPayloadAtomically(sourcePayloadPath, payloadPath);
            WriteTextAtomically(bootstrapPath,
                CreateBootstrapSource(template, installerAssetPath, payloadGuid));
            WriteTextAtomically(markerPath,
                OwnershipMarkerContents + Environment.NewLine);
        }
    }
    private static string CreateBootstrapSource(string template,
        string installerPath, string payloadGuid)
    {
        string suffix = ComputeStableHash(installerPath).Substring(0, 16);
        return template
            .Replace("@@INSTALLER_NAMESPACE@@",
                // A hash may begin with a digit, which cannot start a C#
                // namespace identifier. Keep the deterministic suffix while
                // giving every generated segment a valid leading character.
                "Threadlight.ComponentsBootstrapper.I" + suffix)
            .Replace("@@INSTALLER_ROOT@@", installerPath)
            .Replace("@@INSTALLER_PAYLOAD_GUID@@", payloadGuid);
    }
    private static void ValidateBootstrapTemplate(string template)
    {
        string[] tokens = { "@@INSTALLER_NAMESPACE@@", "@@INSTALLER_ROOT@@",
            "@@INSTALLER_PAYLOAD_GUID@@" };
        foreach (string token in tokens)
        {
            if (!template.Contains(token))
                throw new InvalidDataException("The ThreadLight Components bootstrapper " +
                    "template is missing the required token " + token + ".");
        }
    }
    private static void CopyPayloadAtomically(string sourcePath, string outputPath) =>
        WriteAtomically(outputPath, output =>
        {
            using (FileStream source = File.OpenRead(sourcePath))
                source.CopyTo(output);
        });
    private static void WriteTextAtomically(string outputPath, string contents)
    {
        byte[] bytes = new UTF8Encoding(false).GetBytes(contents);
        WriteAtomically(outputPath, output => output.Write(bytes, 0, bytes.Length));
    }
    private static void WriteAtomically(string outputPath, Action<Stream> write)
    {
        string temporaryPath = outputPath + ".tmp";
        if (File.Exists(temporaryPath))
            File.Delete(temporaryPath);
        try
        {
            using (FileStream output = new FileStream(temporaryPath,
                FileMode.CreateNew, FileAccess.Write, FileShare.None))
                write(output);
            CommitTemporaryFile(temporaryPath, outputPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
    private static void CommitTemporaryFile(string temporaryPath, string outputPath)
    {
        string backupPath = outputPath + ".backup";
        if (!File.Exists(outputPath))
        {
            File.Move(temporaryPath, outputPath);
            TryDeleteBackup(backupPath);
            return;
        }
        if (File.Exists(backupPath))
            File.Delete(backupPath);
        File.Move(outputPath, backupPath);
        try
        {
            File.Move(temporaryPath, outputPath);
        }
        catch
        {
            if (!File.Exists(outputPath) && File.Exists(backupPath))
                File.Move(backupPath, outputPath);
            throw;
        }
        TryDeleteBackup(backupPath);
    }
    private static void TryDeleteBackup(string backupPath)
    {
        try
        {
            if (File.Exists(backupPath))
                File.Delete(backupPath);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("ThreadLight Components left a recoverable temporary " +
                "backup at " + backupPath + ":\n" + exception.Message);
        }
    }
    private static void TryRemoveIncompleteInstaller(bool created, string path)
    {
        if (!created || !AssetDatabase.IsValidFolder(path))
            return;
        try
        {
            AssetDatabase.DeleteAsset(path);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("ThreadLight Components could not remove an incomplete " +
                "temporary installer at " + path + ":\n" + exception.Message);
        }
    }
    private static void WriteAuthoringRetentionMarker(string installerPath)
    {
        string markerPath = AuthoringRetentionMarkerPath(installerPath);
        Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
        File.WriteAllText(markerPath, installerPath, new UTF8Encoding(false));
    }
    private static string AuthoringRetentionMarkerPath(string installerPath)
    {
        string token = ComputeStableHash(installerPath ?? string.Empty);
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "Library", "Threadlight", "Authoring Bootstrap Retention",
            token + ".marker");
    }
    private static bool TryNormalizeFolderPath(string folderPath,
        out string normalizedPath, out string error)
    {
        normalizedPath = (folderPath ?? string.Empty).Replace('\\', '/')
            .Trim().TrimEnd('/');
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedPath))
            error = "Choose a folder inside Assets for the temporary bootstrapper.";
        else if (normalizedPath != "Assets" &&
                 !normalizedPath.StartsWith("Assets/", StringComparison.Ordinal))
            error = "The temporary bootstrapper must be created inside Assets.";
        else
        {
            foreach (string segment in normalizedPath.Split('/'))
            {
                if (string.IsNullOrWhiteSpace(segment) || segment == "." ||
                    segment == ".." ||
                    segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    error = "The bootstrapper folder path is not valid.";
                    break;
                }
            }
        }
        return error.Length == 0;
    }
    private static void EnsureFolderPath(string folderPath)
    {
        string[] segments = folderPath.Split('/');
        string current = segments[0];
        for (int index = 1; index < segments.Length; index++)
        {
            string next = current + "/" + segments[index];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segments[index]);
            current = next;
        }
    }
    private static bool HasOwnershipMarker(string markerPath) =>
        File.Exists(markerPath) && string.Equals(
            File.ReadAllText(markerPath).Trim(), OwnershipMarkerContents,
            StringComparison.Ordinal);
    private static string GetAuthoringCorePackageRoot()
    {
        PackageInfo package = PackageInfo.FindForAssembly(
            typeof(ThreadlightComponentsBootstrapExportUtility).Assembly);
        if (package == null || string.IsNullOrWhiteSpace(package.resolvedPath))
            throw new DirectoryNotFoundException(
                "Could not locate the ThreadLight Authoring package.");
        return package.resolvedPath;
    }
    private static string GetParentAssetPath(string assetPath)
    {
        int separator = assetPath.LastIndexOf('/');
        if (separator <= 0)
            throw new InvalidDataException("The installer path has no parent folder.");
        return assetPath.Substring(0, separator);
    }
    private static string ToAbsolutePath(string assetPath)
    {
        DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
        if (projectRoot == null)
            throw new DirectoryNotFoundException("Could not locate the Unity project root.");
        string absolutePath = Path.GetFullPath(
            Path.Combine(projectRoot.FullName, assetPath));
        return ToFileSystemPath(absolutePath);
    }
    private static string ToFileSystemPath(string absolutePath)
    {
        if (Path.DirectorySeparatorChar != '\\' ||
            absolutePath.StartsWith(@"\\?\", StringComparison.Ordinal))
            return absolutePath;

        // Unity keeps project-relative AssetDatabase paths, while System.IO on
        // Windows still needs the extended prefix once a creator's project and
        // selected export folder cross MAX_PATH.
        return absolutePath.StartsWith(@"\\", StringComparison.Ordinal)
            ? @"\\?\UNC\" + absolutePath.Substring(2)
            : @"\\?\" + absolutePath;
    }
    private static string ComputeStableHash(string value)
    {
        using (SHA256 algorithm = SHA256.Create())
        {
            byte[] hash = algorithm.ComputeHash(
                Encoding.UTF8.GetBytes(value ?? string.Empty));
            StringBuilder output = new StringBuilder(hash.Length * 2);
            foreach (byte valueByte in hash)
                output.Append(valueByte.ToString("x2"));
            return output.ToString();
        }
    }
    private static string EnsureMetaGuid(string assetPath, string importer)
    {
        string metaPath = assetPath + ".meta";
        string guid = string.Empty;
        if (File.Exists(metaPath))
        {
            foreach (string line in File.ReadLines(metaPath))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("guid:", StringComparison.OrdinalIgnoreCase))
                {
                    guid = trimmed.Substring("guid:".Length).Trim();
                    break;
                }
            }
        }
        if (!string.IsNullOrWhiteSpace(guid))
            return guid;
        guid = Guid.NewGuid().ToString("N");
        File.WriteAllText(metaPath, "fileFormatVersion: 2\n" +
            "guid: " + guid + "\n" + importer + "\n" +
            "  externalObjects: {}\n  userData:\n  assetBundleName:\n" +
            "  assetBundleVariant:\n", new UTF8Encoding(false));
        return guid;
    }
}
}
