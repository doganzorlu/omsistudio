using System;
using System.Collections.Generic;

namespace OmsiStudio.App.Services;

public class LocalizationService : ILocalizationService
{
    private string _currentCulture = "tr-TR";

    public event EventHandler? CultureChanged;

    private readonly Dictionary<string, Dictionary<string, string>> _translations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tr-TR"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["AppTitle"] = "OmsiStudio Varlık Tarayıcısı",
            ["SelectRootFolder"] = "OMSI Kök Klasörünü Seç",
            ["SearchPlaceholder"] = "Varlıkları ara...",
            ["GroupingFolder"] = "Klasör",
            ["GroupingCategory"] = "Kategori",
            ["ObjectsFoundFormat"] = "Bulunan Nesne: {0}",
            ["ShowingOfFormat"] = "Gösterilen: {0} / {1}",
            ["ScanErrorFormat"] = "{0} Tarama Hatası",
            ["ScanWarningFormat"] = "{0} Tarama Uyarısı",
            ["ScanCompletedMessages"] = "Tarama mesajlarla tamamlandı.",
            ["ScanningMessage"] = "Sceneryobjects klasörü taranıyor...",
            ["ErrorTitle"] = "Hata Oluştu",
            ["WelcomeTitle"] = "Varlık Tarayıcısına Hoş Geldiniz",
            ["WelcomeMessage"] = "Başlamak için 'OMSI Kök Klasörünü Seç' butonuna tıklayarak OMSI'nin kurulu olduğu dizini seçin.",
            ["SelectedAssetTitle"] = "Seçilen Varlık Özellikleri",
            ["DescriptionSection"] = "Açıklama",
            ["NoDescription"] = "Bu nesne için açıklama bulunmuyor.",
            ["GroupHierarchySection"] = "Grup Hiyerarşisi",
            ["NoGrouping"] = "Herhangi bir gruplama kategorisi belirtilmemiş.",
            ["MeshesSection"] = "İlişkili Mesh Dosyaları (.o3d)",
            ["NoMeshes"] = "Bu .sco dosyasında mesh referansı bulunmuyor.",
            ["O3dMetadataTitle"] = "O3D Model Metadatası",
            ["O3dVersion"] = "Sürüm",
            ["O3dEncrypted"] = "Şifreli",
            ["O3dMeshCount"] = "Mesh Sayısı",
            ["O3dVertexCount"] = "Vertex (Köşe) Sayısı",
            ["O3dTriangleCount"] = "Poligon (Üçgen) Sayısı",
            ["O3dMaterialCount"] = "Materyal Sayısı",
            ["O3dTextureReferences"] = "Kullanılan Kaplamalar",
            ["O3dNoMetadata"] = "O3D metadatası bulunmuyor",
            ["O3dDiagnostics"] = "Hatalar ve Uyarılar",
            ["TexturesSection"] = "İlişkili Kaplama Dosyaları (Texture)",
            ["NoTextures"] = "Bu .sco dosyasında kaplama referansı bulunmuyor.",
            ["NoSelectionTitle"] = "Seçili Nesne Yok",
            ["NoSelectionMessage"] = "Özelliklerini görüntülemek için listeden bir varlık seçin.",
            ["CopyFullPath"] = "Tam Yolu Kopyala",
            ["CopyRelativePath"] = "Göreceli Yolu Kopyala",
            ["OpenFolder"] = "Klasörü Aç",
            ["CopyAssetPathFail"] = "Varlık yolu kopyalanamadı: {0}",
            ["CopyRelativePathFail"] = "Göreceli yol kopyalanamadı: {0}",
            ["OpenFolderFail"] = "Klasör açılamadı: {0}",
            ["DefaultUntitled"] = "(Başlıksız Nesne)",
            ["ScanFailedMessage"] = "Tarama başarısız oldu: {0}",
            ["RelativePathLabel"] = "Göreceli Yol",
            ["FullPathLabel"] = "Tam Yol",
            ["NoFolderSelected"] = "(OMSI klasörü seçilmedi)",
            ["CancelScan"] = "Taramayı İptal Et",
            ["ScanProgressFormat"] = "Taranan: {0} nesne | Son dosya: {1}",
            ["ScanCancelledMessage"] = "Tarama kullanıcı tarafından iptal edildi.",
            ["ExportManifest"] = "Manifest Dışa Aktar",
            ["ExportSuccessFormat"] = "Manifest başarıyla dışa aktarıldı: {0}",
            ["ExportFailFormat"] = "Manifest dışa aktarma başarısız: {0}",
            ["ExportFolderPickFail"] = "Çıktı klasörü seçilemedi: {0}"
        },
        ["en-US"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["AppTitle"] = "OmsiStudio Asset Browser",
            ["SelectRootFolder"] = "Select OMSI Root Folder",
            ["SearchPlaceholder"] = "Search assets...",
            ["GroupingFolder"] = "Folder",
            ["GroupingCategory"] = "Category",
            ["ObjectsFoundFormat"] = "Objects Found: {0}",
            ["ShowingOfFormat"] = "Showing {0} of {1}",
            ["ScanErrorFormat"] = "{0} Scan Error(s)",
            ["ScanWarningFormat"] = "{0} Scan Warning(s)",
            ["ScanCompletedMessages"] = "Scan completed with messages.",
            ["ScanningMessage"] = "Scanning Sceneryobjects recursively...",
            ["ErrorTitle"] = "Error Occurred",
            ["WelcomeTitle"] = "Welcome to Asset Browser",
            ["WelcomeMessage"] = "To begin, click 'Select OMSI Root Folder' and select the directory where OMSI is installed.",
            ["SelectedAssetTitle"] = "Selected Asset Properties",
            ["DescriptionSection"] = "Description",
            ["NoDescription"] = "No description available for this object.",
            ["GroupHierarchySection"] = "Group Hierarchy",
            ["NoGrouping"] = "No grouping categories specified.",
            ["MeshesSection"] = "Associated Meshes (.o3d)",
            ["NoMeshes"] = "No meshes referenced in this .sco file.",
            ["O3dMetadataTitle"] = "O3D Model Metadata",
            ["O3dVersion"] = "Version",
            ["O3dEncrypted"] = "Encrypted",
            ["O3dMeshCount"] = "Mesh Count",
            ["O3dVertexCount"] = "Vertex Count",
            ["O3dTriangleCount"] = "Triangle Count",
            ["O3dMaterialCount"] = "Material Count",
            ["O3dTextureReferences"] = "Referenced Textures",
            ["O3dNoMetadata"] = "No O3D metadata available",
            ["O3dDiagnostics"] = "Errors and Warnings",
            ["TexturesSection"] = "Associated Textures",
            ["NoTextures"] = "No textures referenced in this .sco file.",
            ["NoSelectionTitle"] = "No Object Selected",
            ["NoSelectionMessage"] = "Select an asset from the list to view its properties.",
            ["CopyFullPath"] = "Copy Full Path",
            ["CopyRelativePath"] = "Copy Relative Path",
            ["OpenFolder"] = "Open Folder",
            ["CopyAssetPathFail"] = "Failed to copy asset path: {0}",
            ["CopyRelativePathFail"] = "Failed to copy relative path: {0}",
            ["OpenFolderFail"] = "Failed to open containing folder: {0}",
            ["DefaultUntitled"] = "(Untitled Object)",
            ["ScanFailedMessage"] = "Scan failed: {0}",
            ["RelativePathLabel"] = "Relative Path",
            ["FullPathLabel"] = "Full Path",
            ["NoFolderSelected"] = "(No folder selected)",
            ["CancelScan"] = "Cancel Scan",
            ["ScanProgressFormat"] = "Scanned: {0} objects | Current: {1}",
            ["ScanCancelledMessage"] = "Scanning was cancelled by the user.",
            ["ExportManifest"] = "Export Manifest",
            ["ExportSuccessFormat"] = "Manifest exported successfully: {0}",
            ["ExportFailFormat"] = "Manifest export failed: {0}",
            ["ExportFolderPickFail"] = "Failed to select output folder: {0}"
        }
    };

    public string CurrentCulture => _currentCulture;

    public string this[string key]
    {
        get
        {
            if (_translations.TryGetValue(_currentCulture, out var langDict) && langDict.TryGetValue(key, out var translation))
            {
                return translation;
            }
            if (_translations["tr-TR"].TryGetValue(key, out var trTranslation))
            {
                return trTranslation;
            }
            return key;
        }
    }

    public void SetCulture(string cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return;
        }

        var normalized = cultureName.Equals("en", StringComparison.OrdinalIgnoreCase) || cultureName.StartsWith("en-", StringComparison.OrdinalIgnoreCase)
            ? "en-US"
            : "tr-TR";

        if (!_currentCulture.Equals(normalized, StringComparison.Ordinal))
        {
            _currentCulture = normalized;
            CultureChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
