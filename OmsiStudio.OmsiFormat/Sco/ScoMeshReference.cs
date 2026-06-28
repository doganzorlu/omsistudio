namespace OmsiStudio.OmsiFormat.Sco;

public sealed class ScoMeshReference
{
    public string MeshPath { get; init; } = string.Empty;

    public double PosX { get; set; } = 0.0;
    public double PosY { get; set; } = 0.0;
    public double PosZ { get; set; } = 0.0;

    public double RotX { get; set; } = 0.0;
    public double RotY { get; set; } = 0.0;
    public double RotZ { get; set; } = 0.0;

    public double ScaleX { get; set; } = 1.0;
    public double ScaleY { get; set; } = 1.0;
    public double ScaleZ { get; set; } = 1.0;

    public System.Collections.Generic.List<string> Warnings { get; } = new();

    public ScoMeshReference()
    {
    }

    public ScoMeshReference(string meshPath)
    {
        MeshPath = meshPath;
    }
}
